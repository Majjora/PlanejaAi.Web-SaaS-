using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PlanejaAi.Controllers
{
    [Authorize(Roles = "owner,admin")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? dataInicio, DateTime? dataFim)
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = role == "owner";

            var query = _context.Eventos
                .Include(e => e.EventoItens)
                .Include(e => e.Cliente)
                .AsNoTracking()
                .AsQueryable();

            // ==========================================================
            // PROTEÇÃO DE DADOS: ADMIN SÓ VÊ A PRÓPRIA EMPRESA
            // ==========================================================
            if (!isOwner)
            {
                query = query.Where(e => e.EmpresaId == empIdLogado);
            }

            var todosEventos = await query.ToListAsync();

            // DEFININDO AS DATAS PADRÕES (Se o usuário não filtrou, pega o ano atual)
            var inicio = dataInicio ?? new DateTime(DateTime.Now.Year, 1, 1);
            var fim = dataFim ?? new DateTime(DateTime.Now.Year, 12, 31);

            ViewData["DataInicioAtual"] = inicio.ToString("yyyy-MM-dd");
            ViewData["DataFimAtual"] = fim.ToString("yyyy-MM-dd");

            var model = new DashboardViewModel();

            // ==========================================================
            // FILTRO FINANCEIRO: USA A DATACRIACAO (Data de movimentação)
            // ==========================================================
            var eventosFiltrados = todosEventos
                .Where(e => e.DataCriacao.Date >= inicio.Date && e.DataCriacao.Date <= fim.Date && e.Status != "Cancelado")
                .ToList();

            model.TotalEventosAtivos = eventosFiltrados.Count(e => e.Status == "Em Planejamento");
            model.TotalEventosConcluidos = eventosFiltrados.Count(e => e.Status == "Concluído");
            model.FaturamentoTotal = eventosFiltrados.Sum(e => e.ValorTotalOrcamento);
            model.CustoTotal = eventosFiltrados.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade));

            // =================================================================
            // LÓGICA DO GRÁFICO: GRANULARIDADE DINÂMICA (Horas, Dias, Meses)
            // Agrupando pela DataCriacao
            // =================================================================
            var diffDias = (fim.Date - inicio.Date).TotalDays;

            var faturamentoLista = new List<decimal>();
            var custoLista = new List<decimal>();

            if (diffDias == 0)
            {
                // 1. MESMO DIA: Agrupa de hora em hora (0h às 23h)
                for (int h = 0; h < 24; h++)
                {
                    var eventosDaHora = eventosFiltrados.Where(e => e.DataCriacao.Hour == h).ToList();
                    faturamentoLista.Add(eventosDaHora.Sum(e => e.ValorTotalOrcamento));
                    custoLista.Add(eventosDaHora.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade)));
                }
            }
            else if (diffDias <= 31)
            {
                // 2. ATÉ 1 MÊS: Agrupa dia por dia
                for (var dia = inicio.Date; dia <= fim.Date; dia = dia.AddDays(1))
                {
                    var eventosDoDia = eventosFiltrados.Where(e => e.DataCriacao.Date == dia).ToList();
                    faturamentoLista.Add(eventosDoDia.Sum(e => e.ValorTotalOrcamento));
                    custoLista.Add(eventosDoDia.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade)));
                }
            }
            else
            {
                // 3. MAIS DE 1 MÊS: Agrupa mês por mês (limite de 13 meses por segurança)
                var inicioMes = new DateTime(inicio.Year, inicio.Month, 1);
                var fimMes = new DateTime(fim.Year, fim.Month, 1);
                int limite = 0;

                for (var mes = inicioMes; mes <= fimMes && limite < 13; mes = mes.AddMonths(1))
                {
                    var eventosDoMes = eventosFiltrados.Where(e => e.DataCriacao.Year == mes.Year && e.DataCriacao.Month == mes.Month).ToList();
                    faturamentoLista.Add(eventosDoMes.Sum(e => e.ValorTotalOrcamento));
                    custoLista.Add(eventosDoMes.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade)));
                    limite++;
                }
            }

            // Atribui as listas geradas dinamicamente ao Model
            model.FaturamentoMensal = faturamentoLista.ToList();
            model.CustoMensal = custoLista.ToList();
            // =================================================================

            // ==========================================================
            // PRÓXIMOS EVENTOS: AQUI MANTEMOS A DATA DO EVENTO (FESTA)
            // ==========================================================
            model.ProximosEventos = todosEventos
                .Where(e => e.DataEvento.Date >= DateTime.Today && e.Status == "Em Planejamento")
                .OrderBy(e => e.DataEvento)
                .Take(5)
                .Select(e => new EventoResumo
                {
                    Id = e.Id,
                    Nome = e.Nome,
                    Data = e.DataEvento,
                    StatusExibicao = e.StatusExibicao,
                    NomeCliente = e.Cliente?.Nome ?? "Sem Cliente"
                })
                .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarCsv(DateTime? dataInicio, DateTime? dataFim)
        {
            if (!dataInicio.HasValue || !dataFim.HasValue)
            {
                TempData["Erro"] = "Por favor, selecione um período de início e fim para exportar os dados financeiros.";
                return RedirectToAction(nameof(Index), new { dataInicio, dataFim });
            }

            var inicio = dataInicio.Value.Date;
            var fim = dataFim.Value.Date;

            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = role == "owner";

            var query = _context.Eventos
                .Include(e => e.EventoItens)
                .Include(e => e.Cliente)
                .AsNoTracking()
                .AsQueryable();

            // ==========================================================
            // PROTEÇÃO DE EXPORTAÇÃO: ADMIN SÓ EXPORTA A PRÓPRIA EMPRESA
            // ==========================================================
            if (!isOwner)
            {
                if (empIdLogado == 0)
                {
                    TempData["Erro"] = "Não foi possível identificar sua empresa para exportar.";
                    return RedirectToAction(nameof(Index));
                }
                query = query.Where(e => e.EmpresaId == empIdLogado);
            }

            // ==========================================================
            // FILTRA PELA DATACRIACAO NA HORA DE EXPORTAR O CSV
            // ==========================================================
            var eventos = await query
                .Where(e => e.DataCriacao.Date >= inicio && e.DataCriacao.Date <= fim && e.Status != "Cancelado")
                .OrderBy(e => e.DataCriacao)
                .ToListAsync();

            if (!eventos.Any())
            {
                TempData["Erro"] = "Nenhum evento financeiro encontrado para o período selecionado.";
                return RedirectToAction(nameof(Index), new { dataInicio, dataFim });
            }

            var csv = new StringBuilder();

            // ==========================================================
            // CABEÇALHO DO CSV: DIFERENTE PARA OWNER E ADMIN
            // ==========================================================
            if (isOwner)
            {
                csv.AppendLine("Empresa ID;Nome do Evento;Data Movimentacao;Cliente;Status;Faturamento Bruto;Custo Total;Lucro Estimado");
            }
            else
            {
                csv.AppendLine("Nome do Evento;Data Movimentacao;Cliente;Status;Faturamento Bruto;Custo Total;Lucro Estimado");
            }

            foreach (var ev in eventos)
            {
                var nome = ev.Nome?.Replace(";", ",") ?? "";
                var cliente = ev.Cliente?.Nome?.Replace(";", ",") ?? "Sem Cliente";
                var faturamento = ev.ValorTotalOrcamento;
                var custo = ev.ValorLocalProprio + ev.EventoItens.Sum(i => i.ValorVenda * i.Quantidade);
                var lucro = faturamento - custo;

                // ==========================================================
                // LINHAS DO CSV: IMPRIME A DATACRIACAO (Data da Movimentação)
                // ==========================================================
                if (isOwner)
                {
                    csv.AppendLine($"{ev.EmpresaId};{nome};{ev.DataCriacao:dd/MM/yyyy};{cliente};{ev.Status};{faturamento:F2};{custo:F2};{lucro:F2}");
                }
                else
                {
                    csv.AppendLine($"{nome};{ev.DataCriacao:dd/MM/yyyy};{cliente};{ev.Status};{faturamento:F2};{custo:F2};{lucro:F2}");
                }
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = preamble.Concat(contentBytes).ToArray();

            string nomeArquivo = $"Relatorio_Financeiro_{inicio:yyyyMMdd}_a_{fim:yyyyMMdd}.csv";

            // ==========================================================
            // GERAÇÃO DE LOG DE AUDITORIA
            // ==========================================================
            try
            {
                var log = new Log
                {
                    Data = DateTime.Now,
                    Usuario = User.Identity?.Name ?? "Usuário Desconhecido",
                    Acao = "EXPORT",
                    Tabela = "Dashboard/Financeiro",
                    Descricao = $"Exportou relatório financeiro. Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}. Total: {eventos.Count} registros.",
                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    EmpresaId = empIdLogado == 0 ? null : empIdLogado
                };

                _context.Logs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Ignora silenciosamente o erro no log para não impedir o download do usuário
            }

            return File(bytes, "text/csv", nomeArquivo);
        }
    }
}