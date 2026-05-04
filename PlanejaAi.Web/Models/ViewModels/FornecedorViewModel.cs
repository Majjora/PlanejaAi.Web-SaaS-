using System.ComponentModel.DataAnnotations;

namespace PlanejaAi.Models.ViewModels
{
    public class FornecedorViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(255)]
        public string Nome { get; set; }

        [StringLength(20)]
        public string? Telefone { get; set; }

        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [StringLength(255)]
        public string? Email { get; set; }

        public string? Observacao { get; set; }
    }
}