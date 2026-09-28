using System.ComponentModel.DataAnnotations;

namespace BoraRachar.DTOs.Expense
{
    public sealed class UpdateExpenseDto
    {
        [Required] public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        [Required] public string PaidByMemberId { get; set; } = string.Empty;
        [Required, MinLength(1), MaxLength(50)] public List<string> SplitAmongMemberIds { get; set; } = new();
        [Required, Range(0, int.MaxValue - 1)] public int? Version { get; set; }
    }
}
