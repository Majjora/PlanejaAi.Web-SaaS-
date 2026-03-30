using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PlanejaAi.Controllers
{
    [Authorize]
    public class UsuariosController : Controller
    {
        private readonly AppDbContext _context;

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }


        public async Task<IActionResult> Index(string busca)
        {
            var perfil = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfil != "owner" && perfil != "admin") return RedirectToAction("Index", "Home");

            
            var query = _context.Logins
                .Include(l => l.Funcionario)
                .Include(l => l.Empresa)
                .AsNoTracking();

            if (perfil == "admin")
                query = query.Where(l => l.EmpresaId == empIdLogado);

            ViewData["FiltroAtual"] = busca;
            return View(await query.ToListAsync());
        }

        
        [HttpGet]
        public IActionResult Criar()
        {
            CarregarViewBags();
            return View("Manter", new UsuariosViewModel());
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(UsuariosViewModel model)
        {
            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            
            if (perfilLogado == "admin")
            {
                model.EmpresaId = empIdLogado;
                model.PerfilAcesso = "users";
            }

            try
            {
                
                var func = new Funcionario
                {
                    Nome = model.Nome,
                    Email = model.Email,
                    Cpf = model.Cpf,
                    Cargo = model.Cargo ?? "Colaborador",
                    EmpresaId = model.EmpresaId
                };
                _context.Funcionarios.Add(func);
                await _context.SaveChangesAsync();

                
                var login = new Login
                {
                    FuncionarioId = func.Id,
                    Email = model.Email,
                    
                    Senha = BCrypt.Net.BCrypt.HashPassword(model.Senha),
                    EmpresaId = model.EmpresaId,
                    PerfilAcesso = model.PerfilAcesso ?? "users"
                };
                _context.Logins.Add(login);
                await _context.SaveChangesAsync();

                await RegistrarLog("INSERT", $"Usuário {model.Email} criado", model.EmpresaId);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Erro = "Erro ao cadastrar: " + ex.Message;
                CarregarViewBags();
                return View(model);
            }
        }

        
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var login = await _context.Logins
                .Include(l => l.Funcionario)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (login == null) return NotFound();

            var model = new UsuariosViewModel
            {
                Id = login.Id,
                Nome = login.Funcionario?.Nome,
                Email = login.Email,
                Cpf = login.Funcionario?.Cpf,
                Cargo = login.Funcionario?.Cargo,
                PerfilAcesso = login.PerfilAcesso,
                EmpresaId = login.EmpresaId ?? 0 
            };

            CarregarViewBags();
            return View("Manter", model);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(UsuariosViewModel model)
        {
            
            var loginOriginal = await _context.Logins
                .Include(l => l.Funcionario)
                .FirstOrDefaultAsync(x => x.Id == model.Id);

            if (loginOriginal == null) return NotFound();

            
            loginOriginal.Email = model.Email;
            loginOriginal.PerfilAcesso = model.PerfilAcesso;

            
            if (!string.IsNullOrWhiteSpace(model.Senha))
            {
                
                loginOriginal.Senha = BCrypt.Net.BCrypt.HashPassword(model.Senha);
            }

            
            if (loginOriginal.Funcionario != null)
            {
                loginOriginal.Funcionario.Nome = model.Nome;
                loginOriginal.Funcionario.Cpf = model.Cpf;
                loginOriginal.Funcionario.Cargo = model.Cargo ?? "Colaborador";
                loginOriginal.Funcionario.Email = model.Email; 
            }

            try
            {
                await _context.SaveChangesAsync();
                await RegistrarLog("UPDATE", $"Usuário {model.Email} editado", loginOriginal.EmpresaId ?? 0);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Erro = "Erro ao atualizar: " + ex.Message;
                CarregarViewBags();
                return View(model);
            }
        }

        
        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            
            var login = await _context.Logins
                .Include(l => l.Funcionario)
                .Include(l => l.Empresa)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (login == null) return NotFound();

            
            return View(login);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id)
        {
            var login = await _context.Logins.Include(l => l.Funcionario).FirstOrDefaultAsync(m => m.Id == id);

            if (login != null)
            {
                var emailLog = login.Email;
                var empIdLog = login.EmpresaId ?? 0;

                
                if (login.Funcionario != null) _context.Funcionarios.Remove(login.Funcionario);
                _context.Logins.Remove(login);

                await _context.SaveChangesAsync();
                await RegistrarLog("DELETE", $"Usuário {emailLog} excluído", empIdLog);
            }
            return RedirectToAction(nameof(Index));
        }

        

        private void CarregarViewBags()
        {
            var perfil = User.FindFirstValue(ClaimTypes.Role);
            ViewBag.PerfilLogado = perfil;

            
            if (perfil == "owner")
            {
                ViewBag.Empresas = _context.Empresas.AsNoTracking().ToList() ?? new List<Empresa>();
            }
        }

        private async Task RegistrarLog(string acao, string desc, int empId)
        {
            try
            {
                var log = new Log
                {
                    Acao = acao,
                    Tabela = "login/funcionario",
                    Descricao = desc,
                    Usuario = User.Identity?.Name ?? "Sistema",
                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "::1",
                    Data = DateTime.Now,
                    EmpresaId = empId
                };
                _context.Logs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch {  }
        }
    }
}