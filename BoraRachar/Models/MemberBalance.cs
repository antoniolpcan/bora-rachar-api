namespace BoraRachar.Models
{
    public sealed record MemberBalance(
        string MemberId,
        string MemberName,
        decimal TotalPaid,
        decimal TotalShare,
        decimal Balance
    );
}
