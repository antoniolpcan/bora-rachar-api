namespace BoraRachar.DTOs.Expense
{
    public sealed record MemberBalanceResponseDto(
        string MemberId,
        string MemberName,
        decimal TotalPaid,
        decimal TotalShare,
        decimal Balance
    );
}
