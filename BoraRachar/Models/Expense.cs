namespace BoraRachar.Models
{
    public class Expense
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; }
        public string Title { get; set; } = null!;
        public decimal Amount { get; set; }
        public string PaidByMemberId { get; set; } = string.Empty;
        public List<string> SplitAmongMemberIds { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime;
    }
}
