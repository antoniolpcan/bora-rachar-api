namespace BoraRachar.DTOs.Expense
{
    public sealed record ExpenseResponseDto(
        string Id,
        string Title,
        decimal Amount,
        string PaidByMemberId,
        IReadOnlyList<string> SplitAmongMemberIds,
        DateTime CreatedAt,
        int Version
    );
}
