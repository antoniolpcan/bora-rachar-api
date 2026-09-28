using BoraRachar.Models;

namespace BoraRachar.Services
{
    public interface ISettlementService
    {
        Task<IReadOnlyList<PaymentInstruction>?> GetAsync(string groupId, CancellationToken cancellationToken = default);
    }
}
