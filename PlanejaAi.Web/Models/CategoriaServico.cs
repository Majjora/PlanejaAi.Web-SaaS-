using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{

    [Table("categorias_servico")]
    public class CategoriaServico
    {
        [Key]
        [Column("cat_id")]
        public int Id { get; set; }

        [Required]
        [Column("emp_id")]
        public int EmpresaId { get; set; }

        [Required(ErrorMessage = "O campo Nome da Categoria é obrigatório.")]
        [Column("cat_nome")]
        [StringLength(100, ErrorMessage = "O nome não pode ter mais de 100 caracteres.")]
        public string Nome { get; set; }

        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }

        public ICollection<ProdutoFornecedor> Produtos { get; set; }

        [Column("cat_data_cadastro")] 
        public DateTime DataCadastro { get; set; } = DateTime.Now;
    }
}