using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("fornecedores")]
    public class Fornecedor
    {
        [Key]
        [Column("forn_id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O Nome do Fornecedor é obrigatório.")]
        [StringLength(255, ErrorMessage = "O nome não pode exceder 255 caracteres.")]
        [Column("forn_nome")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "O CNPJ ou CPF é obrigatório.")]
        [StringLength(18, ErrorMessage = "O CNPJ/CPF deve ter no máximo 18 caracteres.")]
        [Column("forn_cnpj_cpf")]
        public string CnpjCpf { get; set; }

        [Required(ErrorMessage = "O E-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido contendo '@'.")]
        [StringLength(255)]
        [Column("forn_email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "O Telefone é obrigatório.")]
        [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
        [Column("forn_telefone")]
        public string Telefone { get; set; }

        [Column("forn_observacao")]
        public string? Observacao { get; set; }

        [Column("forn_status")]
        public bool Status { get; set; } = true;

        [Column("forn_data_cadastro")]
        public DateTime DataCadastro { get; set; }

        [Column("emp_id")]
        public int EmpresaId { get; set; }

        [ValidateNever]
        public virtual Empresa Empresa { get; set; }
    }
}