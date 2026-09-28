using System.ComponentModel.DataAnnotations;

namespace BoraRachar.DTOs.Group
{
    public class CreateGroupDto
    {
        [Required(ErrorMessage = "Informe o nome do grupo.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe os participantes.")]
        [MaxLength(50)]
        [MinLength(2, ErrorMessage = "O grupo deve ter pelo menos dois participantes.")]
        public List<string> Members { get; set; } = new();
    }
}
