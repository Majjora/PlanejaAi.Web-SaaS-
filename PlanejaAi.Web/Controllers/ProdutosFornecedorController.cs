using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
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

        [HttpGet]
        [Authorize(Roles = "owner,admin")]
        public async Task<IActionResult> ExportarCsv()
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = role == "owner";

            if (role != "admin" && role != "owner")
            {
                TempData["Erro"] = "Acesso negado. Apenas Administradores podem exportar dados.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var query = _context.ProdutosFornecedor
                    .Include(p => p.Categoria)
                    .Include(p => p.Fornecedor)
                    .AsNoTracking()
                    .AsQueryable();

                if (!isOwner)
                {
                    query = query.Where(p => p.Fornecedor.EmpresaId == empIdLogado);
                }

                var produtos = await query.OrderBy(p => p.Nome).ToListAsync();

                var csv = new StringBuilder();

                if (isOwner)
                {
                    csv.AppendLine("Empresa ID;Nome;Descrição;Valor Padrão;Unidade;Categoria;Fornecedor;Status;Data de Cadastro");
                }
                else
                {
                    csv.AppendLine("Nome;Descrição;Valor Padrão;Unidade;Categoria;Fornecedor;Status;Data de Cadastro");
                }

                var culturaBr = new System.Globalization.CultureInfo("pt-BR");

                foreach (var produto in produtos)
                {
                    var nome = produto.Nome?.Replace(";", ",").Replace("\r", "").Replace("\n", " ") ?? "";
                    var descricao = produto.Descricao?.Replace(";", ",").Replace("\r", "").Replace("\n", " ") ?? "";
                    var valorPadrao = produto.ValorPadrao?.ToString("F2", culturaBr) ?? "0,00";
                    var unidade = produto.Unidade?.Replace(";", ",") ?? "Unidade";
                    var categoria = produto.Categoria?.Nome?.Replace(";", ",") ?? "Sem Categoria";
                    var fornecedor = produto.Fornecedor?.Nome?.Replace(";", ",") ?? "Sem Fornecedor";

                    var status = produto.Ativo ? "Ativo" : "Inativo";
                    var dataCadastro = produto.DataCadastro.ToString("dd/MM/yyyy HH:mm:ss");

                    if (isOwner)
                    {
                        var empresaId = produto.Fornecedor?.EmpresaId ?? 0;
                        csv.AppendLine($"{empresaId};{nome};{descricao};{valorPadrao};{unidade};{categoria};{fornecedor};{status};{dataCadastro}");
                    }
                    else
                    {
                        csv.AppendLine($"{nome};{descricao};{valorPadrao};{unidade};{categoria};{fornecedor};{status};{dataCadastro}");
                    }
                }

                RegistrarLog("EXPORT", "ProdutosFornecedor", $"Exportação CSV Produtos: {produtos.Count} itens exportados.", empIdLogado);
                await _context.SaveChangesAsync();

                var buffer = Encoding.UTF8.GetBytes(csv.ToString());
                var bom = Encoding.UTF8.GetPreamble();
                var arquivoFinal = bom.Concat(buffer).ToArray();

                var nomeArquivo = $"produtos_exportados_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(arquivoFinal, "text/csv", nomeArquivo);
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro crítico ao gerar o arquivo de exportação: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "owner,admin")]
        public async Task<IActionResult> ImportarCsv(IFormFile arquivoCsv)
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (role != "admin" && role != "owner")
            {
                TempData["Erro"] = "Acesso negado. Apenas Administradores podem importar dados.";
                return RedirectToAction(nameof(Index));
            }

            if (arquivoCsv == null || arquivoCsv.Length == 0)
            {
                TempData["Erro"] = "Por favor, selecione um arquivo CSV válido.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int totalInserido = 0;
                int totalAtualizado = 0;
                int numeroDaLinha = 1;

                var categoriasExistentes = await _context.CategoriasServico
                    .Where(c => c.EmpresaId == empIdLogado)
                    .ToListAsync();

                var fornecedoresExistentes = await _context.Fornecedores
                    .Where(f => f.EmpresaId == empIdLogado)
                    .ToListAsync();

                var produtosExistentes = await _context.ProdutosFornecedor
                    .Include(p => p.Fornecedor)
                    .Where(p => p.Fornecedor.EmpresaId == empIdLogado)
                    .ToListAsync();

                using (var reader = new StreamReader(arquivoCsv.OpenReadStream(), Encoding.UTF8, true))
                {
                    var cabecalho = await reader.ReadLineAsync();

                    if (cabecalho != null && (cabecalho.ToLower().Contains("confirmado") || cabecalho.ToLower().Contains("convidado") || cabecalho.ToLower().Contains("cnpj")))
                    {
                        TempData["Erro"] = "Importação cancelada: O layout do arquivo enviado é incompatível com o modelo esperado para Produtos.";
                        return RedirectToAction(nameof(Index));
                    }

                    while (!reader.EndOfStream)
                    {
                        var linha = await reader.ReadLineAsync();
                        numeroDaLinha++;

                        if (string.IsNullOrWhiteSpace(linha) || string.IsNullOrWhiteSpace(linha.Replace(";", "")))
                        {
                            continue;
                        }

                        var campos = linha.Split(';');
                        if (campos.Length < 6)
                        {
                            TempData["Erro"] = $"Importação cancelada: A linha {numeroDaLinha} está incompleta. Certifique-se de que o arquivo possui todas as 6 colunas necessárias.";
                            return RedirectToAction(nameof(Index));
                        }

                        var nomeProduto = campos[0]?.Trim();
                        var descricao = campos[1]?.Trim();
                        var valorPadraoStr = campos[2]?.Trim();
                        var unidade = campos[3]?.Trim();
                        var nomeCategoriaCsv = campos[4]?.Trim();
                        var nomeFornecedorCsv = campos[5]?.Trim();

                        if (string.IsNullOrEmpty(nomeProduto) || string.IsNullOrEmpty(valorPadraoStr) || string.IsNullOrEmpty(nomeCategoriaCsv) || string.IsNullOrEmpty(nomeFornecedorCsv))
                        {
                            TempData["Erro"] = $"Importação cancelada na linha {numeroDaLinha}: Existem campos obrigatórios (Nome, Valor Padrão, Categoria ou Fornecedor) vazios.";
                            return RedirectToAction(nameof(Index));
                        }

                        if (!decimal.TryParse(valorPadraoStr, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out decimal valorPadrao))
                        {
                            TempData["Erro"] = $"Importação cancelada na linha {numeroDaLinha}: O valor '{valorPadraoStr}' não é um número decimal válido.";
                            return RedirectToAction(nameof(Index));
                        }

                        var categoriaEncontrada = categoriasExistentes
                            .FirstOrDefault(c => c.Nome.Equals(nomeCategoriaCsv, StringComparison.OrdinalIgnoreCase));

                        var fornecedorEncontrado = fornecedoresExistentes
                            .FirstOrDefault(f => f.Nome.Equals(nomeFornecedorCsv, StringComparison.OrdinalIgnoreCase));

                        if (categoriaEncontrada == null)
                        {
                            TempData["Erro"] = $"Importação cancelada: A Categoria '{nomeCategoriaCsv}' (linha {numeroDaLinha}) não foi encontrada no sistema. Cadastre-a antes de importar.";
                            return RedirectToAction(nameof(Index));
                        }

                        if (fornecedorEncontrado == null)
                        {
                            TempData["Erro"] = $"Importação cancelada: O Fornecedor '{nomeFornecedorCsv}' (linha {numeroDaLinha}) não foi encontrado no sistema. Cadastre-o antes de importar.";
                            return RedirectToAction(nameof(Index));
                        }

                        var produtoAtual = produtosExistentes.FirstOrDefault(p =>
                            p.Nome.Equals(nomeProduto, StringComparison.OrdinalIgnoreCase) &&
                            p.FornecedorId == fornecedorEncontrado.Id);

                        if (produtoAtual != null)
                        {
                            produtoAtual.Descricao = descricao;
                            produtoAtual.ValorPadrao = valorPadrao;
                            produtoAtual.Unidade = string.IsNullOrEmpty(unidade) ? produtoAtual.Unidade : unidade;
                            produtoAtual.CategoriaId = categoriaEncontrada.Id;
                            produtoAtual.Ativo = true;

                            _context.ProdutosFornecedor.Update(produtoAtual);
                            totalAtualizado++;
                        }
                        else
                        {
                            var novoProduto = new ProdutoFornecedor
                            {
                                Nome = nomeProduto,
                                Descricao = descricao,
                                ValorPadrao = valorPadrao,
                                Unidade = string.IsNullOrEmpty(unidade) ? "Unidade" : unidade,
                                CategoriaId = categoriaEncontrada.Id,
                                FornecedorId = fornecedorEncontrado.Id,
                                Ativo = true,
                                DataCadastro = DateTime.Now
                            };

                            _context.ProdutosFornecedor.Add(novoProduto);
                            totalInserido++;
                        }
                    }
                }

                if (totalInserido > 0 || totalAtualizado > 0)
                {
                    RegistrarLog("IMPORT", "ProdutosFornecedor", $"Importação CSV Produtos: {totalInserido} inseridos, {totalAtualizado} atualizados.", empIdLogado);

                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = $"Importação finalizada com sucesso! {totalInserido} novos produtos adicionados e {totalAtualizado} atualizados.";
                }
                else
                {
                    TempData["Erro"] = "Nenhum dado válido para importar.";
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro crítico ao processar o arquivo de produtos: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}