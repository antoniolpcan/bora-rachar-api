using BoraRachar.Models;

namespace BoraRachar.Repositories
{
    public interface IGroupRepository
    {
        Task CreateAsync(Group group, CancellationToken cancellationToken = default);
        Task<Group?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<Group?> GetSummaryAsync(string id, CancellationToken cancellationToken = default);
        Task<Group?> GetForExpenseAsync(string id, string expenseId, CancellationToken cancellationToken = default);
        Task<string?> GetAccessHashAsync(string id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Expense>?> GetExpensesAsync(string id, int skip, int limit, CancellationToken cancellationToken = default);
        Task<ExpenseWriteStatus> AddExpenseAsync(string groupId, Expense expense, CancellationToken cancellationToken = default);
        Task<ExpenseWriteStatus> UpdateExpenseAsync(string groupId, Expense expense, int expectedVersion, CancellationToken cancellationToken = default);
        Task<ExpenseWriteStatus> DeleteExpenseAsync(string groupId, string expenseId, int expectedVersion, CancellationToken cancellationToken = default);
    }
}
