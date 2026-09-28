using BoraRachar.Security;
using BoraRachar.Models;
using BoraRachar.Repositories;
using BoraRachar.Services.Results;

namespace BoraRachar.Services
{
    public class GroupService : IGroupService
    {
        private readonly IGroupRepository _groupRepository;

        public GroupService(IGroupRepository groupRepository)
        {
            _groupRepository = groupRepository;
        }

        public async Task<CreateGroupResult> CreateAsync(string name, IReadOnlyCollection<string> memberNames, CancellationToken cancellationToken = default)
        {
            var normalizedName = name?.Trim() ?? string.Empty;

            if (normalizedName.Length is < 2 or > 100)
            {
                return CreateGroupResult.Failure("Name", "O nome do grupo deve ter entre 2 e 100 caracteres.");
            }

            if (memberNames is null || memberNames.Count < 2 || memberNames.Count > GroupRules.MaxMembers)
            {
                return CreateGroupResult.Failure("Members", "O grupo deve ter entre 2 e 50 participantes.");
            }

            if (memberNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Trim().Length > GroupRules.MaxMemberNameLength))
            {
                return CreateGroupResult.Failure("Members", "Cada participante deve ter um nome de até 100 caracteres.");
            }

            var token = GroupTokens.Generate();
            var group = new Group
            {
                Name = normalizedName,
                AccessTokenHash = GroupTokens.Hash(token),
                Members = memberNames.Select(memberName => new Member { Name = memberName.Trim() }).ToList()
            };

            await _groupRepository.CreateAsync(group, cancellationToken);

            return CreateGroupResult.Success(group, token);
        }
        public Task<Group?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return _groupRepository.GetSummaryAsync(id, cancellationToken);
        }
    }
}
