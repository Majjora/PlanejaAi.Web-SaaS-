using System.ComponentModel.DataAnnotations;

namespace PlanejaAi.Models
{
    public class Evento
    {
        [Key] // Diz que isso é a Chave Primária (PK) no banco
        public int IdEvento { get; set; }

        [Required(ErrorMessage = "O nome do evento é obrigatório!")]
        [StringLength(100)]
        public string Nome { get; set; }

        [Required]
        public DateTime DataInicio { get; set; }

        public DateTime DataFim { get; set; }

        [StringLength(200)]
        public string Local { get; set; }

        public string Descricao { get; set; }

        // Depois vamos colocar aqui a relação com Fornecedores e Checklist
    }
}