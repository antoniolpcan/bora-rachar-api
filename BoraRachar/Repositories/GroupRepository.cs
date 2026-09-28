using BoraRachar.Data;
using BoraRachar.Models;
using BoraRachar.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace BoraRachar.Repositories
{
    public sealed class GroupRepository : IGroupRepository
    {
        private readonly IMongoCollection<Group> _groups;
        public GroupRepository(IMongoClient client, IOptions<MongoDbSettings> settings)
            => _groups = client.GetDatabase(settings.Value.DatabaseName).GetCollection<Group>("Groups");

        private static FilterDefinition<Group>? ById(string id) => ObjectId.TryParse(id, out var parsed)
            ? Builders<Group>.Filter.Eq(g => g.Id, parsed.ToString()) : null;

        public Task CreateAsync(Group group, CancellationToken cancellationToken = default)
            => _groups.InsertOneAsync(group, cancellationToken: cancellationToken);

        public async Task<Group?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = ById(id);
            return filter is null ? null : await _groups.Find(filter)
                .Project<Group>(Builders<Group>.Projection.Exclude(g => g.AccessTokenHash))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Group?> GetSummaryAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = ById(id);
            return filter is null ? null : await _groups.Find(filter)
                .Project<Group>(Builders<Group>.Projection.Exclude(g => g.Expenses).Exclude(g => g.AccessTokenHash))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<string?> GetAccessHashAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = ById(id);
            return filter is null ? null : await _groups.Find(filter)
                .Project(g => g.AccessTokenHash).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Group?> GetForExpenseAsync(string id, string expenseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = ById(id);
            return filter is null ? null : await _groups.Find(filter)
                .Project(g => new Group
                {
                    Id = g.Id,
                    Name = g.Name,
                    Members = g.Members,
                    Expenses = g.Expenses.Where(e => e.Id == expenseId).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Expense>?> GetExpensesAsync(string id, int skip, int limit, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = ById(id);
            if (filter is null) return null;
            var group = await _groups.Find(filter)
                .Project<Group>(Builders<Group>.Projection.Include(g => g.Expenses).Slice(g => g.Expenses, skip, limit))
                .FirstOrDefaultAsync(cancellationToken);
            return group?.Expenses;
        }

        public async Task<ExpenseWriteStatus> AddExpenseAsync(string groupId, Expense expense, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = ById(groupId);
            if (id is null) return ExpenseWriteStatus.NotFound;
            var filter = id & Builders<Group>.Filter.Exists($"Expenses.{GroupRules.MaxExpenses - 1}", false);
            var result = await _groups.UpdateOneAsync(filter, Builders<Group>.Update.Push(g => g.Expenses, expense), cancellationToken: cancellationToken);
            if (result.MatchedCount > 0) return ExpenseWriteStatus.Applied;
            return await _groups.Find(id).AnyAsync(cancellationToken)
                ? ExpenseWriteStatus.LimitReached : ExpenseWriteStatus.NotFound;
        }

        private static FilterDefinition<Expense> ExpenseVersion(string expenseId, int expectedVersion)
        {
            var f = Builders<Expense>.Filter;
            var version = f.Eq(e => e.Version, expectedVersion);
            if (expectedVersion == 0) version |= f.Exists(e => e.Version, false);
            return f.Eq(e => e.Id, expenseId) & version;
        }

        public async Task<ExpenseWriteStatus> UpdateExpenseAsync(string groupId, Expense expense, int expectedVersion, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = ById(groupId);
            if (id is null) return ExpenseWriteStatus.NotFound;
            var filter = id & Builders<Group>.Filter.ElemMatch(g => g.Expenses, ExpenseVersion(expense.Id, expectedVersion));
            var update = Builders<Group>.Update
                .Set(g => g.Expenses.FirstMatchingElement().Title, expense.Title)
                .Set(g => g.Expenses.FirstMatchingElement().Amount, expense.Amount)
                .Set(g => g.Expenses.FirstMatchingElement().PaidByMemberId, expense.PaidByMemberId)
                .Set(g => g.Expenses.FirstMatchingElement().SplitAmongMemberIds, expense.SplitAmongMemberIds)
                .Inc(g => g.Expenses.FirstMatchingElement().Version, 1);
            var result = await _groups.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.MatchedCount > 0 ? ExpenseWriteStatus.Applied
                : await MissingOrConflict(id, expense.Id, cancellationToken);
        }

        public async Task<ExpenseWriteStatus> DeleteExpenseAsync(string groupId, string expenseId, int expectedVersion, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = ById(groupId);
            if (id is null) return ExpenseWriteStatus.NotFound;
            var filter = id & Builders<Group>.Filter.ElemMatch(g => g.Expenses, ExpenseVersion(expenseId, expectedVersion));
            var result = await _groups.UpdateOneAsync(filter,
                Builders<Group>.Update.PullFilter(g => g.Expenses, e => e.Id == expenseId), cancellationToken: cancellationToken);
            return result.ModifiedCount > 0 ? ExpenseWriteStatus.Applied
                : await MissingOrConflict(id, expenseId, cancellationToken);
        }

        private async Task<ExpenseWriteStatus> MissingOrConflict(FilterDefinition<Group> id, string expenseId, CancellationToken cancellationToken)
        {
            var exists = await _groups.Find(id & Builders<Group>.Filter.ElemMatch(g => g.Expenses, e => e.Id == expenseId))
                .AnyAsync(cancellationToken);
            return exists ? ExpenseWriteStatus.Conflict : ExpenseWriteStatus.NotFound;
        }
    }
}
