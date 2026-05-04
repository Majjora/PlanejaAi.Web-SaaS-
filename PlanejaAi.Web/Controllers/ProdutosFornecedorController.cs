using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlanejaAi.Controllers
{
    [Authorize] 
    public class ProdutosFornecedorController : Controller
    {
        private readonly AppDbContext _context;

        public ProdutosFornecedorController(AppDbContext context)
        {
            _context = context;
        }

        private void RegistrarLog(string acao, string tabela, string descricao, int? empresaId)
        {
            var usuario = User.Identity?.Name ?? "Sistema";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "N/A";

            var log = new Log
            {
                Acao = acao,
                Tabela = tabela,
                Descricao = descricao,
                Usuario = usuario,
                Ip = ip,
                Data = DateTime.Now,
                EmpresaId = empresaId
            };

            _context.Set<Log>().Add(log);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? fornecedorId, string busca)
        {
            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            var query = _context.ProdutosFornecedor
                        .Include(p => p.Categoria)
                        .Include(p => p.Fornecedor)
                            .ThenInclude(f => f.Empresa)
                        .AsQueryable();

            if (perfilLogado != "owner")
            {
                query = query.Where(p => p.Fornecedor.EmpresaId == empIdLogado);
            }

            if (fornecedorId.HasValue)
            {
                query = query.Where(p => p.FornecedorId == fornecedorId.Value);

                var fornecedor = await _context.Fornecedores.AsNoTracking()
                                               .FirstOrDefaultAsync(f => f.Id == fornecedorId);

                ViewBag.FornecedorNome = fornecedor?.Nome;
                ViewBag.FornecedorId = fornecedorId;
            }
            else
            {
                ViewBag.FornecedorNome = "Todos os Fornecedores";
                ViewBag.FornecedorId = null;
            }

            if (!string.IsNullOrEmpty(busca))
            {
                query = query.Where(p => p.Nome.Contains(busca) || p.Descricao.Contains(busca));
                ViewData["FiltroAtual"] = busca;
            }

            IQueryable<Fornecedor> queryForn = _context.Fornecedores;

            if (perfilLogado != "owner")
            {
                queryForn = queryForn.Where(f => f.EmpresaId == empIdLogado);
            }

            ViewBag.ListaFornecedores = new SelectList(await queryForn.AsNoTracking().ToListAsync(), "Id", "Nome", fornecedorId);

            var listaProdutos = await query.AsNoTracking().ToListAsync();

            ViewBag.EmpresaIdDoFornecedor = empIdLogado;

            return View(listaProdutos);
        }

        private async Task CarregarListasViewBag(int? empresaIdDoFornecedor)
        {
            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            IQueryable<CategoriaServico> queryCat = _context.CategoriasServico;
            IQueryable<Fornecedor> queryForn = _context.Fornecedores;

            if (perfilLogado != "owner")
            {
                queryCat = queryCat.Where(c => c.EmpresaId == empIdLogado);
                queryForn = queryForn.Where(f => f.EmpresaId == empIdLogado);
            }
            else if (empresaIdDoFornecedor.HasValue)
            {
                queryCat = queryCat.Where(c => c.EmpresaId == empresaIdDoFornecedor.Value);
                queryForn = queryForn.Where(f => f.EmpresaId == empresaIdDoFornecedor.Value);
            }

            ViewBag.Categorias = new SelectList(await queryCat.AsNoTracking().ToListAsync(), "Id", "Nome");
            ViewBag.Fornecedores = new SelectList(await queryForn.AsNoTracking().ToListAsync(), "Id", "Nome");
        }

        [HttpGet]
        public async Task<IActionResult> Criar(int? fornecedorId)
        {
            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            var model = new ProdutoFornecedor
            {
                Ativo = true
            };

            if (fornecedorId.HasValue)
            {
                var fornecedor = await _context.Fornecedores.FindAsync(fornecedorId);
                if (fornecedor == null) return NotFound();

                if (perfilLogado != "owner" && fornecedor.EmpresaId != empIdLogado)
                    return RedirectToAction("Index", "Home");

                model.FornecedorId = fornecedorId.Value;
                await CarregarListasViewBag(fornecedor.EmpresaId);
            }
            else
            {
                await CarregarListasViewBag(null);
            }

            return View("Manter", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(ProdutoFornecedor produto, string? fornecedorOrigem)
        {
            produto.DataCadastro = DateTime.Now;

            ModelState.Remove("Categoria");
            ModelState.Remove("Fornecedor");

            if (produto.FornecedorId <= 0)
            {
                ModelState.AddModelError("FornecedorId", "Por favor, selecione um fornecedor.");
            }

            if (!ModelState.IsValid)
            {
                await CarregarListasViewBag(null);
                return View("Manter", produto);
            }

            var fornecedor = await _context.Fornecedores.AsNoTracking().FirstOrDefaultAsync(f => f.Id == produto.FornecedorId);

            if (fornecedor == null)
            {
                ModelState.AddModelError("FornecedorId", "Fornecedor não encontrado no sistema.");
                await CarregarListasViewBag(null);
                return View("Manter", produto);
            }

            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfilLogado != "owner" && fornecedor.EmpresaId != empIdLogado)
                return Unauthorized();

            _context.ProdutosFornecedor.Add(produto);
            await _context.SaveChangesAsync();

            RegistrarLog("CREATE", "ProdutosFornecedor", $"Produto ID: {produto.Id} cadastrado para o Fornecedor ID: {produto.FornecedorId}.", fornecedor.EmpresaId);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Produto cadastrado com sucesso!";

            if (string.IsNullOrEmpty(fornecedorOrigem))
            {
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return RedirectToAction(nameof(Index), new { fornecedorId = fornecedorOrigem });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var produto = await _context.ProdutosFornecedor
                .Include(p => p.Fornecedor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto == null) return NotFound();

            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfilLogado != "owner" && produto.Fornecedor.EmpresaId != empIdLogado)
                return RedirectToAction("Index", "Home");

            await CarregarListasViewBag(produto.Fornecedor.EmpresaId);

            return View("Manter", produto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, ProdutoFornecedor produto, string? fornecedorOrigem)
        {
            if (id != produto.Id) return NotFound();

            ModelState.Remove("Categoria");
            ModelState.Remove("Fornecedor");

            var produtoDb = await _context.ProdutosFornecedor
                .Include(p => p.Fornecedor)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produtoDb == null) return NotFound();

            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfilLogado != "owner" && produtoDb.Fornecedor.EmpresaId != empIdLogado)
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                await CarregarListasViewBag(produtoDb.Fornecedor.EmpresaId);
                return View("Manter", produto);
            }

            _context.Update(produto);
            await _context.SaveChangesAsync();

            RegistrarLog("UPDATE", "ProdutosFornecedor", $"Produto ID: {produto.Id} atualizado.", produtoDb.Fornecedor.EmpresaId);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Produto atualizado com sucesso!";

            if (string.IsNullOrEmpty(fornecedorOrigem))
            {
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return RedirectToAction(nameof(Index), new { fornecedorId = fornecedorOrigem });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "owner,admin")]
        public async Task<IActionResult> Deletar(int id, int fornecedorId, string? fornecedorOrigem)
        {
            var produto = await _context.ProdutosFornecedor.FindAsync(id);
            if (produto == null)
            {
                TempData["Erro"] = "Produto não encontrado.";

                if (string.IsNullOrEmpty(fornecedorOrigem))
                    return RedirectToAction(nameof(Index));
                else
                    return RedirectToAction(nameof(Index), new { fornecedorId });
            }

            var fornecedor = await _context.Fornecedores.FindAsync(produto.FornecedorId);
            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfilLogado != "owner" && fornecedor.EmpresaId != empIdLogado)
                return RedirectToAction("Index", "Home");

            RegistrarLog("DELETE", "ProdutosFornecedor", $"Produto ID: {produto.Id} excluído.", fornecedor.EmpresaId);

            _context.ProdutosFornecedor.Remove(produto);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Produto excluído com sucesso!";

            if (string.IsNullOrEmpty(fornecedorOrigem))
            {
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return RedirectToAction(nameof(Index), new { fornecedorId = fornecedorOrigem });
            }
        }
    }
}