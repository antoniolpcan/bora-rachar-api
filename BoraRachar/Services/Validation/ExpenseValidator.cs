using BoraRachar.Models;

namespace BoraRachar.Services.Validation
{
    public static class ExpenseValidator
    {
        public static ValidationError? ValidateInput(string? title, decimal amount, string? paidByMemberId, IReadOnlyCollection<string>? splitAmongMemberIds)
        {
            var normalizedTitle = title?.Trim() ?? string.Empty;

            if (normalizedTitle.Length is < 2 or > 100)
            {
                return new ValidationError("Title", "O título deve ter entre 2 e 100 caracteres.");
            }

            if (amount <= 0 || amount > ExpenseRules.MaxAmount)
            {
                return new ValidationError("Amount", "O valor deve ser maior que zero e até R$ 1.000.000,00.");
            }

            if (decimal.Round(amount, 2) != amount)
            {
                return new ValidationError("Amount", "O valor deve ter no máximo duas casas decimais.");
            }

            if (string.IsNullOrWhiteSpace(paidByMemberId))
            {
                return new ValidationError("PaidByMemberId", "Informe quem pagou a despesa.");
            }

            if (splitAmongMemberIds is null || splitAmongMemberIds.Count == 0 || splitAmongMemberIds.Count > GroupRules.MaxMembers)
            {
                return new ValidationError("SplitAmongMemberIds", "Selecione entre 1 e 50 participantes.");
            }

            if (splitAmongMemberIds.Any(string.IsNullOrWhiteSpace))
            {
                return new ValidationError("SplitAmongMemberIds", "Os IDs dos participantes não podem estar vazios.");
            }

            var uniqueIds = splitAmongMemberIds.ToHashSet(StringComparer.Ordinal);

            if (uniqueIds.Count != splitAmongMemberIds.Count)
            {
                return new ValidationError("SplitAmongMemberIds", "Não repita participantes na divisão.");
            }

            return null;
        }

        public static ValidationError? ValidateMembers(Group group, string paidByMemberId, IReadOnlyCollection<string> splitAmongMemberIds)
        {
            var memberIds = group.Members
                .Select(member => member.Id)
                .ToHashSet(StringComparer.Ordinal);

            if (!memberIds.Contains(paidByMemberId))
            {
                return new ValidationError("PaidByMemberId", "Quem pagou precisa pertencer ao grupo.");
            }

            if (splitAmongMemberIds.Any(id => !memberIds.Contains(id)))
            {
                return new ValidationError("SplitAmongMemberIds", "Todos os participantes da divisão devem pertencer ao grupo.");
            }

            return null;
        }
    }
}
