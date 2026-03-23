using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("empresas")] 
    public class Empresa
    {
        [Key]
        [Column("emp_id")]
        public int Id { get; set; }

        [Column("emp_nome")]
        public string Nome { get; set; }

        [Column("emp_email")]
        public string Email { get; set; }

        [Column("emp_telefone")]
        public string Telefone { get; set; }

        [Column("emp_cnpj")]
        public string Cnpj { get; set; }
    }
}