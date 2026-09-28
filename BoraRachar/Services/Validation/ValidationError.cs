namespace BoraRachar.Services.Validation
{
    public sealed record ValidationError(
        string Field,
        string Message
    );
}
