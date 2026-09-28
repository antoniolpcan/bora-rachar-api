using BoraRachar.Models;

namespace BoraRachar.Services.Calculations
{
    public static class SettlementCalculator
    {
        public static IReadOnlyList<PaymentInstruction> Calculate(IReadOnlyList<MemberBalance> balances)
        {
            var creditors = balances
                .Where(member => member.Balance > 0)
                .OrderByDescending(member => member.Balance)
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .Select(member => new PendingAmount(
                    member.MemberId,
                    member.Balance))
                .ToList();

            var debtors = balances
                .Where(member => member.Balance < 0)
                .OrderBy(member => member.Balance)
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .Select(member => new PendingAmount(
                    member.MemberId,
                    -member.Balance))
                .ToList();

            if (creditors.Sum(member => member.Amount) != debtors.Sum(member => member.Amount))
            {
                throw new InvalidOperationException("Os saldos do grupo não estão equilibrados.");
            }

            var payments = new List<PaymentInstruction>();

            var debtorIndex = 0;
            var creditorIndex = 0;

            while (debtorIndex < debtors.Count && creditorIndex < creditors.Count)
            {
                var debtor = debtors[debtorIndex];
                var creditor = creditors[creditorIndex];

                var amount = Math.Min(debtor.Amount, creditor.Amount);

                payments.Add(new PaymentInstruction(
                    debtor.MemberId,
                    creditor.MemberId,
                    amount));

                debtor.Amount -= amount;
                creditor.Amount -= amount;

                if (debtor.Amount == 0)
                {
                    debtorIndex++;
                }

                if (creditor.Amount == 0)
                {
                    creditorIndex++;
                }
            }

            return payments;
        }
        private sealed class PendingAmount
        {
            public string MemberId { get; }
            public decimal Amount { get; set; }

            public PendingAmount(string memberId, decimal amount)
            {
                MemberId = memberId;
                Amount = amount;
            }
        }
    }
}
