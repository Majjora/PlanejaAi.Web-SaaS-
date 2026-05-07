using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

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
    }
}