using System;
using System.Collections.Generic;

namespace PlanejaAi.Models
{
    public class DashboardViewModel
    {
        // Cards Superiores
        public int TotalEventosAtivos { get; set; }
        public int TotalEventosConcluidos { get; set; }
        public decimal FaturamentoTotal { get; set; }
        public decimal CustoTotal { get; set; }
        public decimal LucroEstimado => FaturamentoTotal - CustoTotal;

        // Gráficos (12 meses - Janeiro a Dezembro)
        public List<decimal> FaturamentoMensal { get; set; } = new List<decimal>(new decimal[12]);
        public List<decimal> CustoMensal { get; set; } = new List<decimal>(new decimal[12]);

        // Tabela de Próximos Eventos
        public List<EventoResumo> ProximosEventos { get; set; } = new List<EventoResumo>();
    }

    public class EventoResumo
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public DateTime Data { get; set; }
        public string StatusExibicao { get; set; }
        public string NomeCliente { get; set; }
    }
}