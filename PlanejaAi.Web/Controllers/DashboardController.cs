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

            if (!isOwner)
            {
                query = query.Where(e => e.EmpresaId == empIdLogado);
            }

            var todosEventos = await query.ToListAsync();

            var inicio = dataInicio ?? new DateTime(DateTime.Now.Year, 1, 1);
            var fim = dataFim ?? new DateTime(DateTime.Now.Year, 12, 31);

            ViewData["DataInicioAtual"] = inicio.ToString("yyyy-MM-dd");
            ViewData["DataFimAtual"] = fim.ToString("yyyy-MM-dd");

            var model = new DashboardViewModel();

            var eventosDoPeriodo = todosEventos
                .Where(e => e.DataCriacao.Date >= inicio.Date && e.DataCriacao.Date <= fim.Date)
                .ToList();

            model.TotalEventosAtivos = eventosDoPeriodo.Count(e => e.Status == "Em Planejamento");
            model.TotalEventosConcluidos = eventosDoPeriodo.Count(e => e.Status == "Concluído");
            model.TotalEventosCancelados = eventosDoPeriodo.Count(e => e.Status == "Cancelado");

            var eventosFiltrados = eventosDoPeriodo.Where(e => e.Status != "Cancelado").ToList();

            model.FaturamentoTotal = eventosFiltrados.Sum(e => e.ValorTotalOrcamento);
            model.CustoTotal = eventosFiltrados.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade)); ;

            var diffDias = (fim.Date - inicio.Date).TotalDays;

            var faturamentoLista = new List<decimal>();
            var custoLista = new List<decimal>();

            if (diffDias == 0)
            {
                for (int h = 0; h < 24; h++)
                {
                    var eventosDaHora = eventosFiltrados.Where(e => e.DataCriacao.Hour == h).ToList();
                    faturamentoLista.Add(eventosDaHora.Sum(e => e.ValorTotalOrcamento));
                    custoLista.Add(eventosDaHora.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade)));
                }
            }
            else if (diffDias <= 31)
            {
                for (var dia = inicio.Date; dia <= fim.Date; dia = dia.AddDays(1))
                {
                    var eventosDoDia = eventosFiltrados.Where(e => e.DataCriacao.Date == dia).ToList();
                    faturamentoLista.Add(eventosDoDia.Sum(e => e.ValorTotalOrcamento));
                    custoLista.Add(eventosDoDia.Sum(e => e.ValorLocalProprio + e.EventoItens.Sum(i => i.ValorVenda * i.Quantidade)));
                }
            }
            else
            {
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

            model.FaturamentoMensal = faturamentoLista.ToList();
            model.CustoMensal = custoLista.ToList();
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

            if (!isOwner)
            {
                if (empIdLogado == 0)
                {
                    TempData["Erro"] = "Não foi possível identificar sua empresa para exportar.";
                    return RedirectToAction(nameof(Index));
                }
                query = query.Where(e => e.EmpresaId == empIdLogado);
            }

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

            }

            return File(bytes, "text/csv", nomeArquivo);
        }
    }
}