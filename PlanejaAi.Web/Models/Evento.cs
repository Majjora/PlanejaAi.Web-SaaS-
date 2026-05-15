using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("eventos")]
    public class Evento
    {
        [Key]
        [Column("even_id")]
        public int Id { get; set; }

        [Column("even_nome")]
        public string Nome { get; set; }

        [Column("even_tipo")]
        public string? Tipo { get; set; }

        [Column("even_status")]
        public string Status { get; set; } = "Em Planejamento";

        [NotMapped]
        public string StatusExibicao
        {
            get
            {
                if (Status == "Cancelado" || Status == "Concluído") return Status;
                if (DataEvento.Date == DateTime.Today) return "É Hoje! 🎉";
                if (DataEvento.Date < DateTime.Today) return "Pendente de Conclusão";
                return Status;
            }
        }

        [Column("even_data")]
        public DateTime DataEvento { get; set; }

        [Column("emp_id")]
        public int EmpresaId { get; set; }
        public virtual Empresa Empresa { get; set; }

        [Column("clien_id")]
        public int ClienteId { get; set; }

        [Column("even_valor_total_orcamento")]
        public decimal ValorTotalOrcamento { get; set; }

        [Column("even_local_tipo")]
        public int LocalTipo { get; set; }

        [Column("even_nome_local_proprio")]
        public string? NomeLocalProprio { get; set; }

        [Column("even_valor_local_proprio")]
        public decimal ValorLocalProprio { get; set; }

        [Column("even_produto_local_id")]
        public int? ProdutoLocalId { get; set; }

        [ForeignKey("ProdutoLocalId")]
        public virtual ProdutoFornecedor? ProdutoLocal { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        [Column("data_termino")]
        public DateTime? DataTermino { get; set; }

        [ForeignKey("ClienteId")]
        public virtual Cliente? Cliente { get; set; }

        public virtual ICollection<EventoItem> EventoItens { get; set; } = new List<EventoItem>();
        public string? Privacidade { get; set; }
        public int? NumeroConvidados { get; set; }
        public virtual ICollection<Checklist> Checklists { get; set; } = new List<Checklist>();
    }
}