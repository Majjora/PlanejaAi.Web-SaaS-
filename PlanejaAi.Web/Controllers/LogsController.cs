using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanejaAi.Controllers
{
    [Authorize(Roles = "admin,owner,Admin,Owner")]
    public class LogsController : Controller
    {
        private readonly AppDbContext _context;

        public LogsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string busca, DateTime? dataInicio, DateTime? dataFim)
        {
            var empIdClaim = User.FindFirst("EmpresaId")?.Value;
            int empIdLogado = int.TryParse(empIdClaim, out int empId) ? empId : 0;
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            var query = _context.Logs.AsNoTracking().AsQueryable();

            if (!isOwner)
            {
                query = query.Where(l => l.EmpresaId == empIdLogado);
            }

            if (!string.IsNullOrEmpty(busca))
            {
                query = query.Where(l => (l.Usuario != null && l.Usuario.Contains(busca)) ||
                                         (l.Acao != null && l.Acao.Contains(busca)) ||
                                         (l.Tabela != null && l.Tabela.Contains(busca)) ||
                                         (l.Descricao != null && l.Descricao.Contains(busca)));
                ViewData["FiltroAtual"] = busca;
            }

            if (dataInicio.HasValue)
            {
                query = query.Where(l => l.Data >= dataInicio.Value.Date);
                ViewData["DataInicioAtual"] = dataInicio.Value.ToString("yyyy-MM-dd");
            }

            if (dataFim.HasValue)
            {
                var dataFimAjustada = dataFim.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(l => l.Data <= dataFimAjustada);
                ViewData["DataFimAtual"] = dataFim.Value.ToString("yyyy-MM-dd");
            }

            var logs = await query.OrderByDescending(l => l.Data).Take(1000).ToListAsync();

            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarCsv(string busca, DateTime? dataInicio, DateTime? dataFim)
        {
            if (!dataInicio.HasValue || !dataFim.HasValue)
            {
                TempData["Erro"] = "Por favor, selecione um período de início e fim para exportar os logs.";
                return RedirectToAction(nameof(Index), new { busca, dataInicio, dataFim });
            }

            var dataInicioAjustada = dataInicio.Value.Date;
            var dataFimAjustada = dataFim.Value.Date.AddDays(1).AddTicks(-1);

            if ((dataFimAjustada - dataInicioAjustada).TotalDays > 90)
            {
                TempData["Erro"] = "O período máximo permitido para exportação é de 90 dias. Refine sua busca.";
                return RedirectToAction(nameof(Index), new { busca, dataInicio, dataFim });
            }

            var empIdClaim = User.FindFirst("EmpresaId")?.Value;
            int empIdLogado = int.TryParse(empIdClaim, out int empId) ? empId : 0;
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            var query = _context.Logs.AsNoTracking().AsQueryable();

            if (!isOwner)
            {
                if (empIdLogado == 0)
                {
                    TempData["Erro"] = "Não foi possível identificar sua empresa para exportar.";
                    return RedirectToAction(nameof(Index));
                }
                query = query.Where(l => l.EmpresaId == empIdLogado);
            }

            query = query.Where(l => l.Data >= dataInicioAjustada && l.Data <= dataFimAjustada);

            if (!string.IsNullOrEmpty(busca))
            {
                query = query.Where(l => (l.Usuario != null && l.Usuario.Contains(busca)) ||
                                         (l.Acao != null && l.Acao.Contains(busca)) ||
                                         (l.Tabela != null && l.Tabela.Contains(busca)) ||
                                         (l.Descricao != null && l.Descricao.Contains(busca)));
            }

            var logs = await query.OrderByDescending(l => l.Data).Take(50000).ToListAsync();

            if (!logs.Any())
            {
                TempData["Erro"] = "Nenhum log encontrado para o período e filtros selecionados.";
                return RedirectToAction(nameof(Index), new { busca, dataInicio, dataFim });
            }

            var csv = new StringBuilder();

            if (isOwner)
            {
                csv.AppendLine("Data;Hora;Usuario;Acao;Tabela;Descricao;IP;Empresa ID");
            }
            else
            {
                csv.AppendLine("Data;Hora;Usuario;Acao;Tabela;Descricao;IP");
            }

            foreach (var log in logs)
            {
                string descricaoLimpa = log.Descricao?.Replace("\r", "").Replace("\n", " ").Replace(";", ",") ?? "";

                if (isOwner)
                {
                    csv.AppendLine($"{log.Data:dd/MM/yyyy};{log.Data:HH:mm:ss};{log.Usuario};{log.Acao};{log.Tabela};\"{descricaoLimpa}\";{log.Ip};{log.EmpresaId}");
                }
                else
                {
                    csv.AppendLine($"{log.Data:dd/MM/yyyy};{log.Data:HH:mm:ss};{log.Usuario};{log.Acao};{log.Tabela};\"{descricaoLimpa}\";{log.Ip}");
                }
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = preamble.Concat(contentBytes).ToArray();

            string nomeArquivo = $"Auditoria_Logs_{dataInicioAjustada:yyyyMMdd}_a_{dataFimAjustada:yyyyMMdd}.csv";

            return File(bytes, "text/csv", nomeArquivo);
        }
    }
}