using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("produtos_fornecedor")]
    public class ProdutoFornecedor
    {
        [Key]
        [Column("prod_id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O Fornecedor é obrigatório.")]
        [Column("forn_id")]
        public int? FornecedorId { get; set; }

        [Required(ErrorMessage = "A Categoria é obrigatória.")]
        [Column("cat_id")]
        public int? CategoriaId { get; set; }

        [Required(ErrorMessage = "O Nome do Produto/Serviço é obrigatório.")]
        [Column("prod_nome")]
        [StringLength(255)]
        public string Nome { get; set; } = string.Empty;

        [Column("prod_descricao")]
        public string? Descricao { get; set; }

        [Required(ErrorMessage = "O Valor Padrão é obrigatório.")]
        [Column("prod_valor_padrao")]
        public decimal? ValorPadrao { get; set; } 

        [Column("prod_unidade")]
        [StringLength(50)]
        public string? Unidade { get; set; }

        [Column("prod_ativo")]
        public bool Ativo { get; set; } = true;

        [ForeignKey("FornecedorId")]
        public virtual Fornecedor? Fornecedor { get; set; }

        [ForeignKey("CategoriaId")]
        public virtual CategoriaServico? Categoria { get; set; }

        [Column("prod_data_cadastro")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;
    }
}