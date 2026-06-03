using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text;

namespace PlanejaAi.Controllers
{
    [Authorize]
    public class ClientesController : Controller
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        private int GetEmpresaId()
        {
            var empIdClaim = User.FindFirst("EmpresaId")?.Value;
            if (int.TryParse(empIdClaim, out int empId))
            {
                return empId;
            }
            return 0;
        }

        private async Task RegistrarLog(string acao, string descricao)
        {
            try
            {
                var usuarioNome = User.Identity?.Name ?? "Sistema";
                var ipClient = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "::1";
                var empId = GetEmpresaId();

                var novoLog = new Log
                {
                    Acao = acao,
                    Tabela = "clientes",
                    Descricao = descricao,
                    Usuario = usuarioNome,
                    Ip = ipClient,
                    Data = DateTime.Now,
                    EmpresaId = empId > 0 ? empId : null
                };

                _context.Logs.Add(novoLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao registrar log: " + ex.Message);
            }
        }

        public async Task<IActionResult> Index(string busca)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var clientesQuery = _context.Clientes.Include(c => c.Empresa).AsQueryable();

            if (!isOwner)
            {
                clientesQuery = clientesQuery.Where(c => c.EmpresaId == empId);
            }

            if (!string.IsNullOrEmpty(busca))
            {
                clientesQuery = clientesQuery.Where(c => c.Nome.Contains(busca) || c.Documento.Contains(busca));
                ViewData["FiltroAtual"] = busca;
            }

            return View(await clientesQuery.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Manter(int? id)
        {
            if (id == null) return View(new Cliente());

            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var query = _context.Clientes.AsQueryable();

            if (!isOwner)
            {
                query = query.Where(c => c.EmpresaId == empId);
            }

            var cliente = await query.FirstOrDefaultAsync(c => c.Id == id);

            return cliente == null ? NotFound() : View(cliente);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manter(Cliente cliente)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            if (cliente.Id == 0)
            {
                if (cliente.EmpresaId == 0)
                {
                    cliente.EmpresaId = empId;
                }
            }
            else
            {
                var clienteOriginal = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cliente.Id);
                if (clienteOriginal != null)
                {
                    if (!isOwner && clienteOriginal.EmpresaId != empId)
                    {
                        return Unauthorized();
                    }
                    cliente.EmpresaId = clienteOriginal.EmpresaId;
                }
            }

            ModelState.Remove("Empresa");

            if (!string.IsNullOrEmpty(cliente.Documento))
            {
                string docLimpo = Regex.Replace(cliente.Documento, @"[^\d]", "");

                if (docLimpo.Length != 11 && docLimpo.Length != 14)
                {
                    ViewBag.Erro = "O documento deve ser um CPF (11 números) ou CNPJ (14 números).";
                    return View(cliente);
                }
                cliente.Documento = docLimpo;
            }

            if (!string.IsNullOrEmpty(cliente.Cep))
            {
                string cepLimpo = Regex.Replace(cliente.Cep, @"[^\d]", "");
                if (cepLimpo.Length != 8)
                {
                    ViewBag.Erro = "O CEP deve conter exatamente 8 números.";
                    return View(cliente);
                }
                cliente.Cep = cepLimpo;
            }

            if (!string.IsNullOrEmpty(cliente.Telefone))
            {
                string telLimpo = Regex.Replace(cliente.Telefone, @"[^\d]", "");
                if (telLimpo.Length < 10 || telLimpo.Length > 11)
                {
                    ViewBag.Erro = "Telefone inválido. Deve ter 10 ou 11 dígitos (incluindo o DDD).";
                    return View(cliente);
                }
                cliente.Telefone = telLimpo;
            }

            if (!string.IsNullOrEmpty(cliente.Email) && !cliente.Email.Contains("@"))
            {
                ViewBag.Erro = "Por favor, insira um e-mail válido.";
                return View(cliente);
            }

            if (!string.IsNullOrEmpty(cliente.Documento))
            {
                var docDuplicado = await _context.Clientes
                    .AnyAsync(c => c.Documento == cliente.Documento && c.Id != cliente.Id && c.EmpresaId == cliente.EmpresaId);

                if (docDuplicado)
                {
                    ViewBag.Erro = "Este CPF ou CNPJ já está cadastrado para outro cliente.";
                    return View(cliente);
                }
            }

            if (!ModelState.IsValid)
            {
                var erros = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ViewBag.Erro = "Verifique os campos: " + string.Join(" | ", erros);
                return View(cliente);
            }

            try
            {
                if (cliente.Id == 0)
                {
                    cliente.DataCadastro = DateTime.Now;
                    _context.Add(cliente);
                    await _context.SaveChangesAsync();

                    await RegistrarLog("CREATE", $"Cliente {cliente.Nome} (Doc: {cliente.Documento}) cadastrado.");
                    TempData["Sucesso"] = "Cliente cadastrado com sucesso!";
                }
                else
                {
                    _context.Update(cliente);
                    await _context.SaveChangesAsync();

                    await RegistrarLog("UPDATE", $"Dados do cliente {cliente.Nome} atualizados.");
                    TempData["Sucesso"] = "Cliente atualizado com sucesso!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                System.Diagnostics.Debug.WriteLine($"[ERRO DE BANCO] {innerMsg}");
                ViewBag.Erro = $"Erro no banco de dados: {innerMsg}";
                return View(cliente);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO CRÍTICO] ClientesController.Manter: {ex.Message}");
                ViewBag.Erro = "Não foi possível salvar os dados do cliente. Por favor, verifique os campos e tente novamente.";
                return View(cliente);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var query = _context.Clientes.Include(c => c.Empresa).AsNoTracking();

            if (!isOwner) query = query.Where(c => c.EmpresaId == empId);

            var cliente = await query.FirstOrDefaultAsync(m => m.Id == id);

            if (cliente == null) return NotFound();

            return View(cliente);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id)
        {
            if (!User.IsInRole("owner") && !User.IsInRole("admin"))
            {
                TempData["Erro"] = "Acesso negado: Somente proprietários ou administradores podem excluir clientes.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                int empId = GetEmpresaId();
                bool isOwner = User.IsInRole("owner");

                var query = _context.Clientes.AsQueryable();

                if (!isOwner) query = query.Where(c => c.EmpresaId == empId);

                var cliente = await query.FirstOrDefaultAsync(c => c.Id == id);

                if (cliente != null)
                {
                    var nomeCliente = cliente.Nome;
                    _context.Clientes.Remove(cliente);
                    await _context.SaveChangesAsync();
                    await RegistrarLog("DELETE", $"Cliente {nomeCliente} (ID: {id}) removido definitivamente.");
                    TempData["Sucesso"] = "Cliente excluído com sucesso!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO CRÍTICO] ClientesController.Deletar: {ex.Message}");
                TempData["Erro"] = "Ocorreu um erro interno ao tentar excluir o cliente. A operação foi cancelada.";
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
                TempData["Erro"] = "Acesso negado. Apenas Administradores podem exportar a base de clientes.";
                return RedirectToAction(nameof(Index));
            }

            var query = _context.Clientes.AsNoTracking().AsQueryable();

            if (!isOwner)
            {
                if (empIdLogado == 0)
                {
                    TempData["Erro"] = "Não foi possível identificar sua empresa para exportar.";
                    return RedirectToAction(nameof(Index));
                }
                query = query.Where(c => c.EmpresaId == empIdLogado);
            }

            var clientes = await query.OrderBy(c => c.Nome).ToListAsync();

            if (!clientes.Any())
            {
                TempData["Erro"] = "Nenhum cliente encontrado para exportação.";
                return RedirectToAction(nameof(Index));
            }

            var csv = new StringBuilder();

            if (isOwner)
            {
                csv.AppendLine("Empresa ID;Nome;Documento;E-mail;Telefone;CEP;Logradouro;Número;Bairro;Cidade;UF;Data de Cadastro");
            }
            else
            {
                csv.AppendLine("Nome;Documento;E-mail;Telefone;CEP;Logradouro;Número;Bairro;Cidade;UF;Data de Cadastro");
            }

            foreach (var c in clientes)
            {
                string nome = c.Nome?.Replace(";", ",") ?? "";
                string email = c.Email?.Replace(";", ",") ?? "";
                string logradouro = c.Logradouro?.Replace(";", ",") ?? "";
                string numero = c.Numero?.Replace(";", ",") ?? "";
                string bairro = c.Bairro?.Replace(";", ",") ?? "";
                string cidade = c.Cidade?.Replace(";", ",") ?? "";
                string uf = c.Uf?.Replace(";", ",") ?? "";
                string dataCadastro = c.DataCadastro.ToString("dd/MM/yyyy HH:mm");

                string documentoTexto = c.Documento != null ? $"=\"{c.Documento.Trim()}\"" : "";
                string telefoneTexto = c.Telefone != null ? $"=\"{c.Telefone.Replace(";", ",")}\"" : "";
                string cepTexto = c.Cep != null ? $"=\"{c.Cep.Replace(";", ",")}\"" : "";

                if (isOwner)
                {
                    csv.AppendLine($"{c.EmpresaId};{nome};{documentoTexto};{email};{telefoneTexto};{cepTexto};{logradouro};{numero};{bairro};{cidade};{uf};{dataCadastro}");
                }
                else
                {
                    csv.AppendLine($"{nome};{documentoTexto};{email};{telefoneTexto};{cepTexto};{logradouro};{numero};{bairro};{cidade};{uf};{dataCadastro}");
                }
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = preamble.Concat(contentBytes).ToArray();

            string nomeArquivo = $"Relatorio_Clientes_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            await RegistrarLog("EXPORT", $"Exportação de {clientes.Count} clientes realizada.");

            return File(bytes, "text/csv; charset=utf-8", nomeArquivo);
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

                var clientesExistentes = await _context.Clientes
                    .Where(c => c.EmpresaId == empIdLogado)
                    .ToListAsync();

                using (var reader = new StreamReader(arquivoCsv.OpenReadStream(), Encoding.UTF8, true))
                {
                    var cabecalho = await reader.ReadLineAsync();

                    if (cabecalho != null && (cabecalho.ToLower().Contains("convidado") || cabecalho.ToLower().Contains("valor padrão")))
                    {
                        TempData["Erro"] = "Importação cancelada: O layout do arquivo enviado é incompatível com a tabela de Clientes.";
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
                        if (campos.Length < 2) continue;

                        var nome = campos[0]?.Replace("=\"", "").Replace("\"", "").Trim();
                        var documento = campos[1]?.Replace("=\"", "").Replace("\"", "").Trim();
                        var email = campos.Length > 2 ? campos[2]?.Replace("\"", "").Trim() : null;
                        var telefone = campos.Length > 3 ? campos[3]?.Replace("=\"", "").Replace("\"", "").Trim() : null;
                        var cep = campos.Length > 4 ? campos[4]?.Replace("=\"", "").Replace("\"", "").Trim() : null;
                        var logradouro = campos.Length > 5 ? campos[5]?.Replace("\"", "").Trim() : null;
                        var numero = campos.Length > 6 ? campos[6]?.Replace("\"", "").Trim() : null;
                        var bairro = campos.Length > 7 ? campos[7]?.Replace("\"", "").Trim() : null;
                        var cidade = campos.Length > 8 ? campos[8]?.Replace("\"", "").Trim() : null;
                        var uf = campos.Length > 9 ? campos[9]?.Replace("\"", "").Trim() : null;

                        if (string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(documento))
                        {
                            TempData["Erro"] = $"Importação cancelada na linha {numeroDaLinha}: Nome e Documento são campos obrigatórios.";
                            return RedirectToAction(nameof(Index));
                        }

                        if (System.Text.RegularExpressions.Regex.IsMatch(documento, @"[a-zA-Z]"))
                        {
                            TempData["Erro"] = $"Importação cancelada na linha {numeroDaLinha}: O documento '{documento}' possui letras, o que é inválido.";
                            return RedirectToAction(nameof(Index));
                        }

                        if (documento.Contains("***"))
                        {
                            TempData["Erro"] = $"Importação cancelada na linha {numeroDaLinha}: O documento '{documento}' está mascarado (***). Informe o documento completo.";
                            return RedirectToAction(nameof(Index));
                        }

                        var clienteAtual = clientesExistentes.FirstOrDefault(c => c.Documento == documento);

                        if (clienteAtual != null)
                        {
                            clienteAtual.Nome = nome;
                            clienteAtual.Email = string.IsNullOrEmpty(email) ? clienteAtual.Email : email;
                            clienteAtual.Telefone = string.IsNullOrEmpty(telefone) ? clienteAtual.Telefone : telefone;
                            clienteAtual.Cep = string.IsNullOrEmpty(cep) ? clienteAtual.Cep : cep;
                            clienteAtual.Logradouro = string.IsNullOrEmpty(logradouro) ? clienteAtual.Logradouro : logradouro;
                            clienteAtual.Numero = string.IsNullOrEmpty(numero) ? clienteAtual.Numero : numero;
                            clienteAtual.Bairro = string.IsNullOrEmpty(bairro) ? clienteAtual.Bairro : bairro;
                            clienteAtual.Cidade = string.IsNullOrEmpty(cidade) ? clienteAtual.Cidade : cidade;
                            clienteAtual.Uf = string.IsNullOrEmpty(uf) ? clienteAtual.Uf : uf;

                            _context.Clientes.Update(clienteAtual);
                            totalAtualizado++;
                        }
                        else
                        {
                            var novoCliente = new Cliente
                            {
                                Nome = nome,
                                Documento = documento,
                                Email = string.IsNullOrEmpty(email) ? null : email,
                                Telefone = string.IsNullOrEmpty(telefone) ? null : telefone,
                                Cep = string.IsNullOrEmpty(cep) ? null : cep,
                                Logradouro = string.IsNullOrEmpty(logradouro) ? null : logradouro,
                                Numero = string.IsNullOrEmpty(numero) ? null : numero,
                                Bairro = string.IsNullOrEmpty(bairro) ? null : bairro,
                                Cidade = string.IsNullOrEmpty(cidade) ? null : cidade,
                                Uf = string.IsNullOrEmpty(uf) ? null : uf,
                                Status = true,
                                DataCadastro = DateTime.Now,
                                EmpresaId = empIdLogado
                            };

                            _context.Clientes.Add(novoCliente);
                            totalInserido++;
                        }
                    }
                }

                if (totalInserido > 0 || totalAtualizado > 0)
                {
                    await RegistrarLog("IMPORT", $"Importação CSV Clientes: {totalInserido} inseridos, {totalAtualizado} atualizados.");
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = $"Importação finalizada! {totalInserido} novos e {totalAtualizado} atualizados.";
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
    }
}