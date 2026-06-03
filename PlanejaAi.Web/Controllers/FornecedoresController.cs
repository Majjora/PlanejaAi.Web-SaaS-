using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Http;

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
        public async Task<IActionResult> ExportarCsv()
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");
            bool isOwner = role == "owner";

            if (role != "admin" && role != "owner")
            {
                TempData["Erro"] = "Acesso negado. Apenas Administradores podem exportar a base de fornecedores.";
                return RedirectToAction(nameof(Index));
            }

            var query = _context.Fornecedores
                .AsNoTracking()
                .AsQueryable();

            if (!isOwner)
            {
                query = query.Where(f => f.EmpresaId == empIdLogado);
            }

            var fornecedores = await query.OrderBy(f => f.Nome).ToListAsync();

            if (!fornecedores.Any())
            {
                TempData["Erro"] = "Nenhum fornecedor encontrado para exportação.";
                return RedirectToAction(nameof(Index));
            }

            var csv = new StringBuilder();

            if (isOwner)
            {
                csv.AppendLine("Empresa ID;Nome Fantasia / Razão Social;CPF / CNPJ;E-mail;Telefone;Status;Observação;Data de Cadastro");
            }
            else
            {
                csv.AppendLine("Nome Fantasia / Razão Social;CPF / CNPJ;E-mail;Telefone;Status;Observação;Data de Cadastro");
            }

            foreach (var f in fornecedores)
            {
                string nome = f.Nome ?? "";

                string cpfCnpj = !string.IsNullOrEmpty(f.CnpjCpf) ? f.CnpjCpf + "\t" : "";

                string email = f.Email ?? "";
                string telefone = f.Telefone ?? "";

                string status = f.Status ? "Ativo" : "Inativo";

                string observacao = f.Observacao?.Replace("\r", "")?.Replace("\n", " ")?.Replace(";", ",") ?? "";
                string dataCadastro = f.DataCadastro.ToString("dd/MM/yyyy HH:mm:ss");

                if (isOwner)
                {
                    csv.AppendLine($"{f.EmpresaId};{nome};{cpfCnpj};{email};{telefone};{status};{observacao};{dataCadastro}");
                }
                else
                {
                    csv.AppendLine($"{nome};{cpfCnpj};{email};{telefone};{status};{observacao};{dataCadastro}");
                }
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = preamble.Concat(contentBytes).ToArray();

            string nomeArquivo = $"Relatorio_Fornecedores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            var log = CriarLog("EXPORT", $"Exportação de {fornecedores.Count} fornecedores realizada.", empIdLogado);
            _context.Logs.Add(log);
            await _context.SaveChangesAsync();

            return File(bytes, "text/csv; charset=utf-8", nomeArquivo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

                var fornecedoresExistentes = await _context.Fornecedores
                    .Where(f => f.EmpresaId == empIdLogado)
                    .ToListAsync();

                using (var reader = new StreamReader(arquivoCsv.OpenReadStream(), Encoding.UTF8))
                {
                    var cabecalho = await reader.ReadLineAsync();

                    if (cabecalho != null && (cabecalho.ToLower().Contains("confirmado") || cabecalho.ToLower().Contains("convidado")))
                    {
                        TempData["Erro"] = "Importação cancelada: O layout do arquivo enviado é incompatível com o modelo esperado para esta tela.";
                        return RedirectToAction(nameof(Index));
                    }

                    while (!reader.EndOfStream)
                    {
                        var linha = await reader.ReadLineAsync();
                        numeroDaLinha++;

                        if (string.IsNullOrWhiteSpace(linha)) continue;

                        var campos = linha.Split(';');
                        if (campos.Length < 3) continue;

                        var nome = campos[0]?.Trim();
                        var cpfCnpj = campos[1]?.Trim();
                        var email = campos[2]?.Trim();
                        var telefone = campos.Length > 3 ? campos[3]?.Trim() : "Não Informado";

                        if (string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(cpfCnpj))
                        {
                            TempData["Erro"] = "Importação cancelada: Existem campos obrigatórios vazios. Certifique-se de preencher todas as informações necessárias na planilha.";
                            return RedirectToAction(nameof(Index));
                        }

                        if (System.Text.RegularExpressions.Regex.IsMatch(cpfCnpj, @"[a-zA-Z]"))
                        {
                            TempData["Erro"] = "Importação cancelada: O formato dos dados é inválido para este tipo de cadastro. Certifique-se de que selecionou o arquivo correto.";
                            return RedirectToAction(nameof(Index));
                        }

                        var fornecedorAtual = fornecedoresExistentes.FirstOrDefault(f => f.CnpjCpf == cpfCnpj);

                        if (fornecedorAtual != null)
                        {
                            fornecedorAtual.Nome = nome;
                            fornecedorAtual.Email = string.IsNullOrEmpty(email) ? fornecedorAtual.Email : email;

                            if (campos.Length > 3 && !string.IsNullOrEmpty(campos[3]))
                            {
                                fornecedorAtual.Telefone = campos[3].Trim();
                            }

                            _context.Fornecedores.Update(fornecedorAtual);
                            totalAtualizado++;
                        }
                        else
                        {
                            var novoFornecedor = new Fornecedor
                            {
                                Nome = nome,
                                CnpjCpf = cpfCnpj,
                                Email = string.IsNullOrEmpty(email) ? "sem@email.com" : email,
                                Telefone = telefone,
                                Status = true,
                                DataCadastro = DateTime.Now,
                                EmpresaId = empIdLogado
                            };

                            _context.Fornecedores.Add(novoFornecedor);
                            totalInserido++;
                        }
                    }
                }

                if (totalInserido > 0 || totalAtualizado > 0)
                {
                    var log = CriarLog("IMPORT", $"Importação CSV: {totalInserido} inseridos, {totalAtualizado} atualizados.", empIdLogado);
                    _context.Logs.Add(log);

                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = $"Importação finalizada com sucesso! {totalInserido} novos fornecedores adicionados e {totalAtualizado} atualizados.";
                }
                else
                {
                    TempData["Erro"] = "Nenhum dado válido para importar.";
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro crítico ao processar o arquivo: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
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