using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("clientes")]
    public class Cliente
    {
        [Key]
        [Column("clien_id")]
        public int Id { get; set; }

        [Column("emp_id")]
        public int EmpresaId { get; set; }

        [Column("clien_nome")]
        public string Nome { get; set; }

        [Column("clien_documento")]
        public string Documento { get; set; }

        [Column("clien_email")]
        public string? Email { get; set; }

        [Column("clien_telefone")]
        public string? Telefone { get; set; }

        [Column("clien_cep")]
        public string? Cep { get; set; }

        [Column("clien_logradouro")]
        public string? Logradouro { get; set; }

        [Column("clien_numero")]
        public string? Numero { get; set; }

        [Column("clien_bairro")]
        public string? Bairro { get; set; }

        [Column("clien_cidade")]
        public string? Cidade { get; set; }

        [Column("clien_uf")]
        public string? Uf { get; set; }

        [Column("clien_status")]
        public bool Status { get; set; } = true;

        [Column("clien_data")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public virtual Empresa? Empresa { get; set; }
    }
}