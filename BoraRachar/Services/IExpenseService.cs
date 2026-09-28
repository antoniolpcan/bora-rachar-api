using BoraRachar.Services.Results;
using BoraRachar.Models;
using BoraRachar.Repositories;

namespace BoraRachar.Services
{
    public interface IExpenseService
    {
        Task<CreateExpenseResult> CreateAsync(string groupId, string title, decimal amount, string paidByMemberId, IReadOnlyCollection<string> splitAmongMemberIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Expense>?> GetAllAsync(string groupId, int page = 1, int pageSize = GroupRules.DefaultPageSize, CancellationToken cancellationToken = default);
        Task<Expense?> GetByIdAsync(string groupId, string expenseId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MemberBalance>?> GetBalancesAsync(string groupId, CancellationToken cancellationToken = default);
        Task<UpdateExpenseResult> UpdateAsync(string groupId, string expenseId, string title, decimal amount, string paidByMemberId, IReadOnlyCollection<string> splitAmongMemberIds, int expectedVersion = 0, CancellationToken cancellationToken = default);
        Task<ExpenseWriteStatus> DeleteAsync(string groupId, string expenseId, int expectedVersion = 0, CancellationToken cancellationToken = default);
    }
}
