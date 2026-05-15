using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("evento_itens")]
    public class EventoItem
    {
        [Key]
        [Column("evit_id")]
        public int Id { get; set; }

        [Column("even_id")]
        public int EventoId { get; set; }

        [Column("prod_id")]
        public int? ProdutoId { get; set; }

        [Column("evit_nome_exibicao")]
        public string? NomeExibicao { get; set; }

        [Column("evit_quantidade")]
        public int Quantidade { get; set; }

        [Column("evit_valor_custo")]
        public decimal ValorCusto { get; set; }

        [Column("evit_valor_venda")]
        public decimal ValorVenda { get; set; }

        [NotMapped]
        public string Descricao => NomeExibicao ?? "Item sem nome";

        [NotMapped]
        public decimal Valor => ValorVenda;

        [Column("evit_status")]
        public string Status { get; set; } = "Pendente";

        [NotMapped]
        public string Categoria { get; set; } = "Geral";

        [ForeignKey("EventoId")]
        public virtual Evento? Evento { get; set; }
    }
}