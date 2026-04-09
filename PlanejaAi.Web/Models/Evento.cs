using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("eventos")] 
    public class Evento
    {
        [Key]
        [Column("even_id")] 
        public int IdEvento { get; set; }

        [Required(ErrorMessage = "O nome do evento é obrigatório!")]
        [Column("even_nome")] 
        public string Nome { get; set; }

        [Column("even_data")]
        public DateTime Data { get; set; }

        [Column("even_local")]
        public string? Local { get; set; }

        [Column("even_status")]
        public string? Status { get; set; }

        [Column("emp_id")]
        public int? EmpresaId { get; set; }

        [ForeignKey("EmpresaId")]
        public virtual Empresa? Empresa { get; set; }
    }
}