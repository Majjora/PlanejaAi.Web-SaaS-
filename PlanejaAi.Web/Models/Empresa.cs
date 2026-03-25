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
        public string? Email { get; set; } // O '?' evita o erro de DBNull

        [Column("emp_telefone")]
        public string? Telefone { get; set; }

        [Column("emp_cnpj")]
        public string? Cnpj { get; set; }

        [Column("emp_data_cadastro")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        [Column("emp_status")]
        public string? Status { get; set; } = "Ativa";

        [Column("emp_cep")]
        public string? Cep { get; set; }

        [Column("emp_endereco")]
        public string? Endereco { get; set; }

        [Column("emp_numero")]
        public string? Numero { get; set; }

        [Column("emp_bairro")]
        public string? Bairro { get; set; }

        [Column("emp_cidade")]
        public string? Cidade { get; set; }

        [Column("emp_estado")]
        public string? Estado { get; set; }
    }
}