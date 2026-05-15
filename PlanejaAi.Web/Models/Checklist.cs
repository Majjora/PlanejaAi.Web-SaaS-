using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("checklist")]
    public class Checklist
    {
        [Key]
        [Column("check_id")]
        public int Id { get; set; }

        [Column("even_id")]
        public int EventoId { get; set; }

        [Column("check_descricao")]
        public string Descricao { get; set; }

        [Column("check_concluido")]
        public bool Concluido { get; set; } = false;

        [ForeignKey("EventoId")]
        public virtual Evento Evento { get; set; }
    }
}