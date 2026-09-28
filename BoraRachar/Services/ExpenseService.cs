using BoraRachar.Models;
using BoraRachar.Repositories;
using BoraRachar.Services.Calculations;
using BoraRachar.Services.Results;
using BoraRachar.Services.Validation;

namespace BoraRachar.Services
{
    public sealed class ExpenseService(IGroupRepository repository) : IExpenseService
    {
        public async Task<CreateExpenseResult> CreateAsync(string groupId, string title, decimal amount, string paidByMemberId, IReadOnlyCollection<string> splitAmongMemberIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var error = ExpenseValidator.ValidateInput(title, amount, paidByMemberId, splitAmongMemberIds);
            if (error is not null) return CreateExpenseResult.ValidationFailure(error.Field, error.Message);
            var group = await repository.GetSummaryAsync(groupId, cancellationToken);
            if (group is null) return CreateExpenseResult.GroupNotFound();
            error = ExpenseValidator.ValidateMembers(group, paidByMemberId, splitAmongMemberIds);
            if (error is not null) return CreateExpenseResult.ValidationFailure(error.Field, error.Message);
            var expense = new Expense
            {
                Title = title.Trim(),
                Amount = amount,
                PaidByMemberId = paidByMemberId,
                SplitAmongMemberIds = splitAmongMemberIds.ToList(),
                Version = 1
            };
            var result = await repository.AddExpenseAsync(group.Id!, expense, cancellationToken);
            return result switch
            {
                ExpenseWriteStatus.Applied => CreateExpenseResult.Success(expense),
                ExpenseWriteStatus.LimitReached => CreateExpenseResult.LimitReached(),
                _ => CreateExpenseResult.GroupNotFound()
            };
        }

        public Task<IReadOnlyList<Expense>?> GetAllAsync(string groupId, int page = 1, int pageSize = GroupRules.DefaultPageSize, CancellationToken cancellationToken = default)
        {
            if (page < 1 || page > GroupRules.MaxExpenses || pageSize < 1 || pageSize > GroupRules.MaxPageSize)
                throw new ArgumentOutOfRangeException(nameof(page), "Paginação inválida.");
            return repository.GetExpensesAsync(groupId, (page - 1) * pageSize, pageSize, cancellationToken);
        }

        public async Task<Expense?> GetByIdAsync(string groupId, string expenseId, CancellationToken cancellationToken = default)
        {
            var group = await repository.GetForExpenseAsync(groupId, expenseId, cancellationToken);
            return group?.Expenses.FirstOrDefault(e => e.Id == expenseId);
        }

        public async Task<IReadOnlyList<MemberBalance>?> GetBalancesAsync(string groupId, CancellationToken cancellationToken = default)
        {
            var group = await repository.GetByIdAsync(groupId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return group is null ? null : BalanceCalculator.Calculate(group);
        }

        public async Task<UpdateExpenseResult> UpdateAsync(string groupId, string expenseId, string title, decimal amount, string paidByMemberId, IReadOnlyCollection<string> splitAmongMemberIds, int expectedVersion = 0, CancellationToken cancellationToken = default)
        {
            var group = await repository.GetForExpenseAsync(groupId, expenseId, cancellationToken);
            var existing = group?.Expenses.FirstOrDefault(e => e.Id == expenseId);
            if (group is null || existing is null) return UpdateExpenseResult.NotFound();
            if (expectedVersion < 0 || expectedVersion == int.MaxValue || existing.Version != expectedVersion)
                return UpdateExpenseResult.Conflict();
            var error = ExpenseValidator.ValidateInput(title, amount, paidByMemberId, splitAmongMemberIds);
            if (error is not null) return UpdateExpenseResult.ValidationFailure(error.Field, error.Message);
            error = ExpenseValidator.ValidateMembers(group, paidByMemberId, splitAmongMemberIds);
            if (error is not null) return UpdateExpenseResult.ValidationFailure(error.Field, error.Message);
            var expense = new Expense
            {
                Id = existing.Id,
                CreatedAt = existing.CreatedAt,
                Version = expectedVersion + 1,
                Title = title.Trim(),
                Amount = amount,
                PaidByMemberId = paidByMemberId,
                SplitAmongMemberIds = splitAmongMemberIds.ToList()
            };
            var result = await repository.UpdateExpenseAsync(group.Id!, expense, expectedVersion, cancellationToken);
            return result switch
            {
                ExpenseWriteStatus.Applied => UpdateExpenseResult.Success(expense),
                ExpenseWriteStatus.Conflict => UpdateExpenseResult.Conflict(),
                _ => UpdateExpenseResult.NotFound()
            };
        }

        public Task<ExpenseWriteStatus> DeleteAsync(string groupId, string expenseId, int expectedVersion = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(expenseId)) return Task.FromResult(ExpenseWriteStatus.NotFound);
            if (expectedVersion < 0) return Task.FromResult(ExpenseWriteStatus.Conflict);
            return repository.DeleteExpenseAsync(groupId, expenseId, expectedVersion, cancellationToken);
        }
    }
}
