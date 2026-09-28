using BoraRachar.Models;
using BoraRachar.Repositories;
using BoraRachar.Services;

namespace BoraRachar.Tests
{
    internal class TestGroupRepository : IGroupRepository
    {
        public Group? ReturnedGroup { get; set; }
        public bool AddResult { get; set; } = true;
        public int GetCalls { get; private set; }
        public int AddCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public string? LastGroupId { get; private set; }
        public Expense? SavedExpense { get; private set; }
        public Group? SavedGroup { get; private set; }
        public Task CreateAsync(Group group, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCalls++;
            group.Id ??= "507f1f77bcf86cd799439011";
            SavedGroup = ReturnedGroup = group;
            return Task.CompletedTask;
        }
        public Task<Group?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCalls++;
            return Task.FromResult(ReturnedGroup?.Id == id ? ReturnedGroup : null);
        }
        public Task<Group?> GetSummaryAsync(string id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task<Group?> GetForExpenseAsync(string id, string expenseId, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public async Task<string?> GetAccessHashAsync(string id, CancellationToken cancellationToken = default)
            => (await GetByIdAsync(id, cancellationToken))?.AccessTokenHash;
        public async Task<IReadOnlyList<Expense>?> GetExpensesAsync(string id, int skip, int limit, CancellationToken cancellationToken = default)
            => (await GetByIdAsync(id, cancellationToken))?.Expenses.Skip(skip).Take(limit).ToArray();
        public Task<ExpenseWriteStatus> AddExpenseAsync(string groupId, Expense expense, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddCalls++;
            LastGroupId = groupId;
            if (!AddResult || ReturnedGroup?.Id != groupId) return Task.FromResult(ExpenseWriteStatus.NotFound);
            if (ReturnedGroup.Expenses.Count >= GroupRules.MaxExpenses) return Task.FromResult(ExpenseWriteStatus.LimitReached);
            SavedExpense = expense;
            ReturnedGroup.Expenses.Add(expense);
            return Task.FromResult(ExpenseWriteStatus.Applied);
        }
        public Task<ExpenseWriteStatus> UpdateExpenseAsync(string groupId, Expense expense, int expectedVersion, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = ReturnedGroup?.Id == groupId ? ReturnedGroup.Expenses.FindIndex(e => e.Id == expense.Id) : -1;
            if (index < 0) return Task.FromResult(ExpenseWriteStatus.NotFound);
            if (ReturnedGroup!.Expenses[index].Version != expectedVersion) return Task.FromResult(ExpenseWriteStatus.Conflict);
            ReturnedGroup.Expenses[index] = expense;
            return Task.FromResult(ExpenseWriteStatus.Applied);
        }
        public Task<ExpenseWriteStatus> DeleteExpenseAsync(string groupId, string expenseId, int expectedVersion, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = ReturnedGroup?.Id == groupId ? ReturnedGroup.Expenses.FirstOrDefault(e => e.Id == expenseId) : null;
            if (existing is null) return Task.FromResult(ExpenseWriteStatus.NotFound);
            if (existing.Version != expectedVersion) return Task.FromResult(ExpenseWriteStatus.Conflict);
            ReturnedGroup!.Expenses.Remove(existing);
            return Task.FromResult(ExpenseWriteStatus.Applied);
        }
    }
}
