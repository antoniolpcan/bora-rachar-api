namespace BoraRachar.Models
{
    public sealed record PaymentInstruction(
        string FromMemberId,
        string ToMemberId,
        decimal Amount
    );
}
