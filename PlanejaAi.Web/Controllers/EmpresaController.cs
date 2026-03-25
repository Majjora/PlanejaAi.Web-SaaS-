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
                var ipClient = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "::1";
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
            // Começamos com a query base
            var empresasQuery = _context.Empresas.AsQueryable();

            // Se houver algo escrito na busca, filtramos por Nome ou CNPJ
            if (!string.IsNullOrEmpty(busca))
            {
                empresasQuery = empresasQuery.Where(e => e.Nome.Contains(busca) || e.Cnpj.Contains(busca));
                ViewData["FiltroAtual"] = busca; // Mantém o texto na barra após pesquisar
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
                .Take(5) // Limita a 5 sugestões para ser rápido
                .ToListAsync();

            return Json(sugestoes);
        }

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
            // --- VALIDAÇÕES DE FORMATO (REGEX) ---

            // 1. Validação de CNPJ (Limpeza e Tamanho)
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

            // 2. Validação de CEP
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

            // 3. Validação de Telefone
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

            // 4. Validação de E-mail básico
            if (string.IsNullOrEmpty(empresa.Email) || !empresa.Email.Contains("@"))
            {
                ViewBag.Erro = "Por favor, insira um e-mail válido.";
                return View(empresa);
            }

            // --- VALIDAÇÃO DE REGRA DE NEGÓCIO (DUPLICIDADE) ---

            // Verifica se já existe outra empresa com o mesmo CNPJ no banco
            // e.Id != empresa.Id garante que, se for uma EDIÇÃO, ele não barrei a própria empresa
            var cnpjDuplicado = await _context.Empresas
                .AnyAsync(e => e.Cnpj == empresa.Cnpj && e.Id != empresa.Id);

            if (cnpjDuplicado)
            {
                ViewBag.Erro = "Este CNPJ já está cadastrado para outra empresa.";
                return View(empresa);
            }

            try
            {
                if (empresa.Id == 0)
                {
                    // Nova Empresa
                    empresa.DataCadastro = DateTime.Now;
                    _context.Add(empresa);
                    await _context.SaveChangesAsync();
                    await RegistrarLog("Inserção", $"Empresa {empresa.Nome} (CNPJ: {empresa.Cnpj}) cadastrada.");
                }
                else
                {
                    // Atualizar Empresa
                    _context.Update(empresa);
                    await _context.SaveChangesAsync();
                    await RegistrarLog("Edição", $"Dados da empresa {empresa.Nome} atualizados.");
                }

                return RedirectToAction(nameof(Cadastro));
            }
            catch (Exception ex)
            {
                ViewBag.Erro = "Erro ao salvar no banco de dados: " + ex.Message;
                return View(empresa);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Deletar(int id)
        {
            var empresa = await _context.Empresas.FindAsync(id);
            if (empresa != null)
            {
                var nomeEmpresa = empresa.Nome;
                _context.Empresas.Remove(empresa);
                await _context.SaveChangesAsync();
                await RegistrarLog("Exclusão", $"Empresa {nomeEmpresa} (ID: {id}) removida do sistema.");
            }
            return RedirectToAction(nameof(Cadastro));
        }
    }

}