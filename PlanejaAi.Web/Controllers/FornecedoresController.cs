using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;

namespace PlanejaAi.Controllers
{
    [Authorize]
    public class FornecedoresController : Controller
    {
        private readonly AppDbContext _context;

        public FornecedoresController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string busca)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            var query = _context.Fornecedores
                                .Include(f => f.Empresa) 
                                .AsNoTracking()
                                .AsQueryable();

            if (!isOwner)
            {
                query = query.Where(f => f.EmpresaId == empIdLogado);
            }

            if (!string.IsNullOrEmpty(busca))
            {
                query = query.Where(f => f.Nome.Contains(busca) ||
                                         (f.Email != null && f.Email.Contains(busca)) ||
                                         (f.CnpjCpf != null && f.CnpjCpf.Contains(busca)));
            }

            ViewData["FiltroAtual"] = busca;

            return View(await query.ToListAsync());
        }

        [HttpGet]
        public IActionResult Criar()
        {
            return View("Manter", new Fornecedor());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(Fornecedor model)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            model.EmpresaId = empIdLogado;
            model.DataCadastro = DateTime.Now;

            ModelState.Remove("EmpresaId");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Fornecedores.Add(model);

                    var log = CriarLog("CREATE", $"Fornecedor '{model.Nome}' cadastrado.", empIdLogado);
                    _context.Logs.Add(log);

                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Fornecedor cadastrado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Erro"] = "Erro ao cadastrar: " + ex.Message;
                }
            }
            return View("Manter", model);
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            var query = _context.Fornecedores.AsQueryable();

            if (!isOwner)
            {
                query = query.Where(f => f.EmpresaId == empIdLogado);
            }

            var fornecedor = await query.FirstOrDefaultAsync(f => f.Id == id);

            if (fornecedor == null) return NotFound();

            return View("Manter", fornecedor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Fornecedor model)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            ModelState.Remove("EmpresaId");

            if (ModelState.IsValid)
            {
                try
                {
                    var fornecedorOriginal = await _context.Fornecedores.AsNoTracking().FirstOrDefaultAsync(f => f.Id == model.Id);

                    if (fornecedorOriginal == null) return NotFound();

                    if (!isOwner && fornecedorOriginal.EmpresaId != empIdLogado) return Unauthorized();

                    model.EmpresaId = fornecedorOriginal.EmpresaId;

                    _context.Fornecedores.Update(model);

                    var log = CriarLog("UPDATE", $"Fornecedor ID {model.Id} ('{model.Nome}') atualizado.", empIdLogado);
                    _context.Logs.Add(log);

                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Fornecedor atualizado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Erro"] = "Erro ao atualizar: " + ex.Message;
                }
            }
            return View("Manter", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            var query = _context.Fornecedores.AsQueryable();

            if (!isOwner)
            {
                query = query.Where(f => f.EmpresaId == empIdLogado);
            }

            var fornecedor = await query.FirstOrDefaultAsync(f => f.Id == id);

            if (fornecedor != null)
            {
                try
                {
                    _context.Fornecedores.Remove(fornecedor);

                    var log = CriarLog("DELETE", $"Fornecedor ID {fornecedor.Id} ('{fornecedor.Nome}') excluído do sistema.", empIdLogado);
                    _context.Logs.Add(log);

                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Fornecedor excluído com sucesso!";
                }
                catch (Exception)
                {
                    TempData["Erro"] = "Não foi possível excluir este fornecedor pois ele já possui vínculos no sistema.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private Log CriarLog(string acao, string descricao, int empId)
        {
            return new Log
            {
                Acao = acao,
                Tabela = "fornecedores",
                Descricao = descricao,
                Usuario = User.Identity?.Name ?? "Sistema",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Desconhecido",
                Data = DateTime.Now,
                EmpresaId = empId
            };
        }
    }
}