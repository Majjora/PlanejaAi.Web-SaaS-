using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanejaAi.Models
{
    [Table("logs")]
    public class Log
    {
        [Key]
        [Column("log_id")]
        public int Id { get; set; }

        [Column("log_acao")]
        public string Acao { get; set; }

        [Column("log_tabela")]
        public string Tabela { get; set; }

        [Column("log_descricao")]
        public string Descricao { get; set; }

        [Column("log_usuario")]
        public string Usuario { get; set; }

        [Column("log_ip")]
        public string Ip { get; set; }

        [Column("log_data")]
        public DateTime Data { get; set; }

        [Column("emp_id")]
        public int? EmpresaId { get; set; }
    }
}