using BoraRachar.Models;
using BoraRachar.Services.Calculations;

namespace BoraRachar.Services
{
    public sealed class SettlementService(IExpenseService expenseService) : ISettlementService
    {
        public async Task<IReadOnlyList<PaymentInstruction>?> GetAsync(string groupId, CancellationToken cancellationToken = default)
        {
            var balances = await expenseService.GetBalancesAsync(groupId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return balances is null ? null : SettlementCalculator.Calculate(balances);
        }
    }
}
