using BoraRachar.Models;

namespace BoraRachar.Services.Calculations
{
    public static class BalanceCalculator
    {
        public static IReadOnlyList<MemberBalance> Calculate(Group group)
        {
            var paid = group.Members.ToDictionary(
                member => member.Id,
                _ => 0m);

            var shares = group.Members.ToDictionary(
                member => member.Id,
                _ => 0m);

            foreach (var expense in group.Expenses)
            {
                paid[expense.PaidByMemberId] += expense.Amount;

                var totalCents = expense.Amount * 100m;
                var participantCount = expense.SplitAmongMemberIds.Count;

                var baseShareCents = decimal.Floor(
                    totalCents / participantCount);

                var remainingCents =
                    totalCents - baseShareCents * participantCount;

                for (var index = 0; index < participantCount; index++)
                {
                    var memberId = expense.SplitAmongMemberIds[index];

                    var shareCents = baseShareCents
                        + (index < remainingCents ? 1m : 0m);

                    shares[memberId] += shareCents / 100m;
                }
            }

            return group.Members
                .Select(member => new MemberBalance(
                    member.Id,
                    member.Name,
                    paid[member.Id],
                    shares[member.Id],
                    paid[member.Id] - shares[member.Id]))
                .ToArray();
        }
    }
}
