using System.ComponentModel.DataAnnotations;

namespace PlanejaAi.Models
{
    public class UsuariosViewModel
    {
        public int? Id { get; set; } 

        [Required(ErrorMessage = "Nome é obrigatório")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "E-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; }

        [Required(ErrorMessage = "CPF é obrigatório")]
        public string Cpf { get; set; }

        public string Cargo { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória")]
        [DataType(DataType.Password)]
        public string Senha { get; set; }

        [Required(ErrorMessage = "Selecione o perfil")]
        public string PerfilAcesso { get; set; }

        [Required(ErrorMessage = "Selecione a empresa")]
        public int EmpresaId { get; set; }
    }
}