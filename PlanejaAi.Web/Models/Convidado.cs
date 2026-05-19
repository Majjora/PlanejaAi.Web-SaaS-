using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("convidados")]
    public class Convidado
    {
        [Key]
        [Column("conv_id")]
        public int Id { get; set; }

        [Column("even_id")]
        public int EventoId { get; set; }

        [Column("conv_nome")]
        public string Nome { get; set; }

        [Column("conv_confirmacao")]
        public int Confirmacao { get; set; } = 0;

        [Column("conv_data_cadastro")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;
    }
}