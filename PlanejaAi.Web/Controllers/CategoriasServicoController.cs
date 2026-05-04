using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System; 
using System.Security.Claims;

namespace PlanejaAi.Controllers
{
    [Authorize]
    public class CategoriasServicoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoriasServicoController> _logger;

        public CategoriasServicoController(AppDbContext context, ILogger<CategoriasServicoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            var isOwner = User.IsInRole("owner"); 

            IQueryable<CategoriaServico> query = _context.CategoriasServico.Include(c => c.Empresa);

            if (!isOwner)
            {
                if (empIdLogado == 0)
                {
                    _logger.LogWarning("Usuário comum sem EmpresaId tentando acessar categorias.");
                    TempData["Erro"] = "Sua conta não possui uma empresa vinculada.";
                    return RedirectToAction("Index", "Home");
                }
                query = query.Where(c => c.EmpresaId == empIdLogado);
            }

            var categorias = await query.AsNoTracking().ToListAsync();
            return View(categorias);
        }

        [HttpGet]
        public IActionResult Criar()
        {
            return View("Manter", new CategoriaServico());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(CategoriaServico categoria)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (empIdLogado == 0)
            {
                _logger.LogWarning($"Falha de segurança: Tentativa de criar categoria por usuário {User.Identity?.Name} sem EmpresaId.");
                return RedirectToAction("Index", "Home");
            }

            categoria.EmpresaId = empIdLogado;
            categoria.DataCadastro = DateTime.Now;

            ModelState.Remove("EmpresaId");
            ModelState.Remove("Empresa");
            ModelState.Remove("ProdutosFornecedor");
            ModelState.Remove("Produtos");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.CategoriasServico.Add(categoria);
                    await _context.SaveChangesAsync();

                    _context.Add(new Log
                    {
                        Acao = "CREATE",
                        Tabela = "CategoriasServico",
                        Descricao = $"Categoria '{categoria.Nome}' (Id: {categoria.Id}) criada.",
                        Usuario = User.Identity?.Name ?? "Desconhecido",
                        Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Data = DateTime.Now,
                        EmpresaId = empIdLogado
                    });
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"SUCESSO: Categoria '{categoria.Nome}' (Id: {categoria.Id}) criada pela EmpresaId {empIdLogado}.");
                    TempData["Sucesso"] = "Categoria criada com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"ERRO DB: Falha ao salvar categoria para a EmpresaId {empIdLogado}.");
                    TempData["Erro"] = "Ocorreu um erro interno ao salvar a categoria.";
                }
            }
            else
            {
                var erros = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning($"FALHA VALIDAÇÃO: ModelState inválido ao criar categoria. Erros: {erros}");
            }

            return View("Manter", categoria);
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            var isOwner = User.IsInRole("owner");

            var categoria = await _context.CategoriasServico
                .FirstOrDefaultAsync(c => c.Id == id && (isOwner || c.EmpresaId == empIdLogado));

            if (categoria == null)
            {
                return NotFound();
            }

            return View("Manter", categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, CategoriaServico categoria)
        {
            if (id != categoria.Id) return NotFound();

            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            categoria.EmpresaId = empIdLogado;

            ModelState.Remove("EmpresaId");
            ModelState.Remove("Empresa");
            ModelState.Remove("ProdutosFornecedor");
            ModelState.Remove("Produtos");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(categoria);
                    await _context.SaveChangesAsync();

                    _context.Add(new Log
                    {
                        Acao = "UPDATE",
                        Tabela = "CategoriasServico",
                        Descricao = $"Categoria Id {categoria.Id} editada para '{categoria.Nome}'.",
                        Usuario = User.Identity?.Name ?? "Desconhecido",
                        Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Data = DateTime.Now,
                        EmpresaId = empIdLogado
                    });
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"SUCESSO: Categoria Id {categoria.Id} editada pela EmpresaId {empIdLogado}.");
                    TempData["Sucesso"] = "Categoria atualizada com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"ERRO DB: Falha ao atualizar categoria {categoria.Id}.");
                    TempData["Erro"] = "Ocorreu um erro interno ao atualizar.";
                }
            }

            return View("Manter", categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "owner,admin")] 
        public async Task<IActionResult> Deletar(int id)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            var isOwner = User.IsInRole("owner");

            var categoria = await _context.CategoriasServico
                .FirstOrDefaultAsync(c => c.Id == id && (isOwner || c.EmpresaId == empIdLogado));

            if (categoria == null)
            {
                _logger.LogWarning($"FALHA EXCLUSÃO: Categoria {id} não encontrada ou sem permissão para o usuário {User.Identity?.Name}.");
                TempData["Erro"] = "Categoria não encontrada ou você não tem permissão para excluí-la.";
                return RedirectToAction(nameof(Index));
            }

            var temProdutos = await _context.ProdutosFornecedor.AnyAsync(p => p.CategoriaId == id);
            if (temProdutos)
            {
                _logger.LogInformation($"EXCLUSÃO BLOQUEADA: Categoria {id} possui produtos vinculados.");
                TempData["Erro"] = "Não é possível excluir esta categoria pois existem produtos vinculados a ela.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var nomeCategoriaExcluida = categoria.Nome;
                var empresaDonaDaCategoria = categoria.EmpresaId;

                _context.CategoriasServico.Remove(categoria);

                _context.Add(new Log
                {
                    Acao = "DELETE",
                    Tabela = "CategoriasServico",
                    Descricao = $"Categoria '{nomeCategoriaExcluida}' (Id: {id}) da Empresa ID {empresaDonaDaCategoria} foi excluída.",
                    Usuario = User.Identity?.Name ?? "Desconhecido",
                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Data = DateTime.Now,
                    EmpresaId = empIdLogado 
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation($"SUCESSO: Categoria Id {id} excluída por {User.Identity?.Name}.");
                TempData["Sucesso"] = "Categoria excluída com sucesso!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ERRO DB: Falha ao excluir categoria {id}.");
                TempData["Erro"] = "Ocorreu um erro interno ao excluir a categoria.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}