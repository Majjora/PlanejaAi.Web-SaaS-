using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("login")]
    public class Login
    {
        [Key]
        [Column("login_id")]
        public int Id { get; set; }

        [Column("func_id")]
        public int? FuncionarioId { get; set; }

        [Column("login_email")]
        public string? Email { get; set; }
        [Column("login_senha")]
        public string? Senha { get; set; }

        [Column("emp_id")]
        public int? EmpresaId { get; set; }

        [Column("perfil_acesso")]
        public string? PerfilAcesso { get; set; } 

        [Column("login_token")]
        public string? Token { get; set; } 

        
        [ForeignKey("FuncionarioId")]
        public virtual Funcionario? Funcionario { get; set; }

        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }

        [Column("login_data_cadastro")]
        public DateTime? DataCadastro { get; set; }
    }
}