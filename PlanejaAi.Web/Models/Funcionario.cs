using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("funcionarios")]
    public class Funcionario
    {
        [Key]
        [Column("func_id")]
        public int Id { get; set; }

        [Column("func_nome")]
        public string? Nome { get; set; }

        [Column("func_email")]
        public string? Email { get; set; }

        [Column("func_cpf")]
        public string? Cpf { get; set; }

        [Column("func_cargo")]
        public string? Cargo { get; set; }

        [Column("emp_id")]
        public int? EmpresaId { get; set; }

        
        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }
    }
}