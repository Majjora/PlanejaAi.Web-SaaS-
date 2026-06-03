using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System;
using System.Security.Claims;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;

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

        [HttpGet]
        [Authorize(Roles = "owner,admin")]
        public async Task<IActionResult> ExportarCsv()
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            var isOwner = role == "owner";

            var empIdClaim = User.FindFirst("EmpresaId")?.Value ?? User.FindFirstValue("EmpresaId");
            int empIdLogado = int.TryParse(empIdClaim, out int empId) ? empId : 0;

            if (role != "admin" && role != "owner")
            {
                TempData["Erro"] = "Acesso negado. Apenas Administradores podem exportar a base de categorias.";
                return RedirectToAction(nameof(Index));
            }

            var query = _context.CategoriasServico.AsQueryable();

            if (!isOwner)
            {
                if (empIdLogado == 0)
                {
                    TempData["Erro"] = "Não foi possível identificar sua empresa para exportar.";
                    return RedirectToAction(nameof(Index));
                }
                query = query.Where(c => c.EmpresaId == empIdLogado);
            }

            var categorias = await query.AsNoTracking().ToListAsync();

            if (!categorias.Any())
            {
                TempData["Erro"] = "Nenhuma categoria encontrada para exportação.";
                return RedirectToAction(nameof(Index));
            }

            var builder = new StringBuilder();

            if (isOwner)
            {
                builder.AppendLine("Empresa ID;Nome;Data de Cadastro");
            }
            else
            {
                builder.AppendLine("Nome;Data de Cadastro");
            }

            foreach (var cat in categorias)
            {
                var nome = cat.Nome?.Replace(";", ",") ?? "";
                var data = cat.DataCadastro.ToString("dd/MM/yyyy HH:mm");

                if (isOwner)
                {
                    builder.AppendLine($"{cat.EmpresaId};{nome};{data}");
                }
                else
                {
                    builder.AppendLine($"{nome};{data}");
                }
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(builder.ToString());
            var bytes = preamble.Concat(contentBytes).ToArray();

            string nomeArquivo = $"Relatorio_Categorias_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv; charset=utf-8", nomeArquivo);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "owner,admin")]
        public async Task<IActionResult> ImportarCsv(IFormFile arquivoCsv)
        {
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (empIdLogado == 0)
            {
                TempData["Erro"] = "Empresa não identificada. Ação bloqueada.";
                return RedirectToAction(nameof(Index));
            }

            if (arquivoCsv == null || arquivoCsv.Length == 0)
            {
                TempData["Erro"] = "Por favor, selecione um arquivo CSV válido.";
                return RedirectToAction(nameof(Index));
            }

            if (!Path.GetExtension(arquivoCsv.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Formato inválido! O arquivo deve ser do tipo .CSV.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int totalInserido = 0;
                int totalAtualizado = 0;

                var categoriasExistentes = await _context.CategoriasServico
                    .Where(c => c.EmpresaId == empIdLogado)
                    .ToListAsync();

                using (var reader = new StreamReader(arquivoCsv.OpenReadStream(), Encoding.UTF8))
                {
                    var cabecalho = await reader.ReadLineAsync();

                    if (cabecalho != null)
                    {
                        var cabecalhoLower = cabecalho.ToLower();
                        if (cabecalhoLower.Contains("confirmado") ||
                            cabecalhoLower.Contains("convidado") ||
                            cabecalhoLower.Contains("cnpj") ||
                            cabecalhoLower.Contains("cpf"))
                        {
                            TempData["Erro"] = "Importação cancelada: O layout do arquivo enviado é incompatível com o modelo esperado para Categorias.";
                            return RedirectToAction(nameof(Index));
                        }
                    }

                    while (!reader.EndOfStream)
                    {
                        var linha = await reader.ReadLineAsync();

                        if (string.IsNullOrWhiteSpace(linha)) continue;

                        var campos = linha.Split(';');
                        if (campos.Length < 1) continue;

                        var nomeCategoria = campos[0]?.Trim();

                        if (string.IsNullOrEmpty(nomeCategoria))
                        {
                            continue;
                        }

                        var categoriaAtual = categoriasExistentes.FirstOrDefault(c => c.Nome.Equals(nomeCategoria, StringComparison.OrdinalIgnoreCase));

                        if (categoriaAtual != null)
                        {
                            categoriaAtual.Nome = nomeCategoria;

                            _context.CategoriasServico.Update(categoriaAtual);
                            totalAtualizado++;
                        }
                        else
                        {
                            var novaCategoria = new CategoriaServico
                            {
                                Nome = nomeCategoria,
                                EmpresaId = empIdLogado,
                                DataCadastro = DateTime.Now
                            };

                            _context.CategoriasServico.Add(novaCategoria);
                            totalInserido++;
                        }
                    }
                }

                if (totalInserido > 0 || totalAtualizado > 0)
                {
                    _context.Add(new Log
                    {
                        Acao = "IMPORT",
                        Tabela = "CategoriasServico",
                        Descricao = $"Importação CSV: {totalInserido} inseridas, {totalAtualizado} atualizadas.",
                        Usuario = User.Identity?.Name ?? "Desconhecido",
                        Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Data = DateTime.Now,
                        EmpresaId = empIdLogado
                    });

                    await _context.SaveChangesAsync();

                    TempData["Sucesso"] = $"Importação finalizada com sucesso! {totalInserido} novas categorias adicionadas e {totalAtualizado} atualizadas.";
                }
                else
                {
                    TempData["Erro"] = "Nenhum dado válido para importar.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ERRO IMPORTAÇÃO CSV CATEGORIAS: Falha na EmpresaId {empIdLogado}.");
                TempData["Erro"] = "Erro crítico ao processar o arquivo. Verifique o formato e tente novamente.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}