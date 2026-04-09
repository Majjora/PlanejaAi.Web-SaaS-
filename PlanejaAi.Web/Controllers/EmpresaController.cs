using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PlanejaAi.Controllers
{
    [Authorize(Roles = "owner")]
    public class EmpresaController : Controller
    {
        private readonly AppDbContext _context;

        public EmpresaController(AppDbContext context)
        {
            _context = context;
        }

        private async Task RegistrarLog(string acao, string descricao)
        {
            try
            {
                var usuarioNome = User.Identity?.Name ?? "Sistema";
                var ipClient = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "::1";
                var empIdClaim = User.FindFirst("EmpresaId")?.Value;

                int? empId = null;
                if (int.TryParse(empIdClaim, out int idResult))
                {
                    empId = idResult;
                }

                var novoLog = new Log
                {
                    Acao = acao,
                    Tabela = "empresas",
                    Descricao = descricao,
                    Usuario = usuarioNome,
                    Ip = ipClient,
                    Data = DateTime.Now,
                    EmpresaId = empId
                };

                _context.Logs.Add(novoLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao registrar log: " + ex.Message);
            }
        }

        public async Task<IActionResult> Cadastro(string busca)
        {

            var empresasQuery = _context.Empresas.AsQueryable();


            if (!string.IsNullOrEmpty(busca))
            {
                empresasQuery = empresasQuery.Where(e => e.Nome.Contains(busca) || e.Cnpj.Contains(busca));
                ViewData["FiltroAtual"] = busca;
            }

            var lista = await empresasQuery.ToListAsync();
            return View(lista);
        }

        [HttpGet]
        public async Task<JsonResult> GetSugestoes(string termo)
        {
            var sugestoes = await _context.Empresas
                .Where(e => e.Nome.Contains(termo))
                .Select(e => e.Nome)
                .Take(5)
                .ToListAsync();

            return Json(sugestoes);
        }

        [HttpGet]
        public async Task<IActionResult> Manter(int? id)
        {
            if (id == null) return View(new Empresa());
            var empresa = await _context.Empresas.FindAsync(id);
            return empresa == null ? NotFound() : View(empresa);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manter(Empresa empresa)
        {

            if (!string.IsNullOrEmpty(empresa.Cnpj))
            {
                string cnpjLimpo = Regex.Replace(empresa.Cnpj, @"[^\d]", "");
                if (cnpjLimpo.Length != 14)
                {
                    ViewBag.Erro = "O CNPJ deve conter exatamente 14 números.";
                    return View(empresa);
                }
                empresa.Cnpj = cnpjLimpo;
            }


            if (!string.IsNullOrEmpty(empresa.Cep))
            {
                string cepLimpo = Regex.Replace(empresa.Cep, @"[^\d]", "");
                if (cepLimpo.Length != 8)
                {
                    ViewBag.Erro = "O CEP deve conter 8 números.";
                    return View(empresa);
                }
                empresa.Cep = cepLimpo;
            }


            if (!string.IsNullOrEmpty(empresa.Telefone))
            {
                string telLimpo = Regex.Replace(empresa.Telefone, @"[^\d]", "");
                if (telLimpo.Length < 10 || telLimpo.Length > 11)
                {
                    ViewBag.Erro = "Telefone inválido. Deve ter 10 ou 11 dígitos (com DDD).";
                    return View(empresa);
                }
                empresa.Telefone = telLimpo;
            }


            if (string.IsNullOrEmpty(empresa.Email) || !empresa.Email.Contains("@"))
            {
                ViewBag.Erro = "Por favor, insira um e-mail válido.";
                return View(empresa);
            }


            var cnpjDuplicado = await _context.Empresas
                .AnyAsync(e => e.Cnpj == empresa.Cnpj && e.Id != empresa.Id);

            if (cnpjDuplicado)
            {
                ViewBag.Erro = "Este CNPJ já está cadastrado.";
                return View(empresa);
            }

            try
            {
                if (empresa.Id == 0)
                {

                    empresa.DataCadastro = DateTime.Now;
                    _context.Add(empresa);
                    await _context.SaveChangesAsync();
                    await RegistrarLog("INSERT", $"Empresa {empresa.Nome} (CNPJ: {empresa.Cnpj}) cadastrada.");
                }
                else
                {

                    _context.Update(empresa);
                    await _context.SaveChangesAsync();
                    await RegistrarLog("UPDATE", $"Dados da empresa {empresa.Nome} atualizados.");
                }

                return RedirectToAction(nameof(Cadastro));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO CRÍTICO] EmpresaController.Manter: {ex.Message}");

                ViewBag.Erro = "Não foi possível salvar os dados da empresa. Por favor, tente novamente em instantes ou contate o suporte.";
                return View(empresa);
            }
        }


        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {

            var empresa = await _context.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (empresa == null) return NotFound();


            return View(empresa);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id)
        {
            try
            {
                var temUsuarios = await _context.Logins.AnyAsync(u => u.EmpresaId == id);
                if (temUsuarios)
                {
                    TempData["Erro"] = "Não é possível excluir: existem usuários vinculados.";
                    return RedirectToAction(nameof(Cadastro));
                }
                var temEventos = await _context.Eventos.AnyAsync(e => e.EmpresaId == id);
                if (temEventos)
                {
                    TempData["Erro"] = "Não é possível excluir: existem eventos cadastrados.";
                    return RedirectToAction(nameof(Cadastro));
                }

                var empresa = await _context.Empresas.FindAsync(id);
                if (empresa != null)
                {
                    var nomeEmpresa = empresa.Nome;

                    await _context.Database.ExecuteSqlRawAsync("UPDATE logs SET emp_id = NULL WHERE emp_id = {0}", id);

                    _context.Empresas.Remove(empresa);
                    await _context.SaveChangesAsync();

                    var logExclusao = new Log
                    {
                        Acao = "DELETE",
                        Tabela = "empresas",
                        Descricao = $"Empresa {nomeEmpresa} (ID: {id}) removida definitivamente.",
                        Usuario = User.Identity?.Name ?? "Sistema",
                        Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido",
                        Data = DateTime.Now,
                        EmpresaId = null 
                    };
                    _context.Logs.Add(logExclusao);
                    await _context.SaveChangesAsync();

                    TempData["Sucesso"] = "Empresa excluída com sucesso!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO CRÍTICO] EmpresaController.Deletar: {ex.Message}");

                TempData["Erro"] = "Ocorreu um erro interno ao tentar excluir a empresa. A operação foi cancelada.";
            }

            return RedirectToAction(nameof(Cadastro));
        }
    }
}