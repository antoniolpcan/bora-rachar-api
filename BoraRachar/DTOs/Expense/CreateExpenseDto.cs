using System.ComponentModel.DataAnnotations;

namespace BoraRachar.DTOs.Expense
{
    public sealed class CreateExpenseDto
    {
        [Required(ErrorMessage = "Informe o título da despesa.")]
        public string Title { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Informe quem pagou a despesa.")]
        public string PaidByMemberId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe os participantes da divisão.")]
        [MaxLength(50)]
        [MinLength(1, ErrorMessage = "Selecione pelo menos um participante.")]
        public List<string> SplitAmongMemberIds { get; set; } = new();
    }
}
