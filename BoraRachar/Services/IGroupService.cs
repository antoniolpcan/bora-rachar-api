using BoraRachar.Models;
using BoraRachar.Services.Results;

namespace BoraRachar.Services
{
    public interface IGroupService
    {
        Task<CreateGroupResult> CreateAsync(string name, IReadOnlyCollection<string> memberNames, CancellationToken cancellationToken = default);
        Task<Group?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    }
}
