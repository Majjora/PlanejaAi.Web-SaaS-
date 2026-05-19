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

        public async Task<IActionResult> Index(string busca)
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

            var logs = await query.OrderByDescending(l => l.Data).ToListAsync();

            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> ExportarCsv()
        {
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

            var logs = await query.OrderByDescending(l => l.Data).Take(5000).ToListAsync();

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
                string descricaoLimpa = log.Descricao?.Replace("\r", "").Replace("\n", " ") ?? "";

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

            string nomeArquivo = $"Auditoria_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", nomeArquivo);
        }
    }
}