using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PlanejaAi.Controllers
{
    [Authorize(Roles = "owner,admin")]
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
                if (string.IsNullOrEmpty(model.PerfilAcesso) || model.PerfilAcesso == "owner")
                {
                    model.PerfilAcesso = "users";
                }

                ModelState.Remove("PerfilAcesso");
            }

            if (!ModelState.IsValid)
            {
                CarregarViewBags();
                return View("Manter", model);
            }

            bool emailJaExiste = await _context.Logins.AnyAsync(l => l.Email == model.Email);
            if (emailJaExiste)
            {
                ViewBag.Erro = "Este e-mail já está cadastrado em outra conta. Por favor, utilize um e-mail diferente.";
                CarregarViewBags();
                return View("Manter", model);
            }

            bool nomeJaExisteNaEmpresa = await _context.Logins
                .AnyAsync(l => l.EmpresaId == model.EmpresaId
                            && l.Funcionario != null
                            && l.Funcionario.Nome == model.Nome);

            if (nomeJaExisteNaEmpresa)
            {
                ViewBag.Erro = $"Atenção: Já existe um colaborador chamado '{model.Nome}' cadastrado na sua empresa.";
                CarregarViewBags();
                return View("Manter", model);
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
                    PerfilAcesso = model.PerfilAcesso ?? "users",
                    DataCadastro = DateTime.Now
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

            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfilLogado == "admin" && login.EmpresaId != empIdLogado)
            {
                return RedirectToAction("Index", "Home"); 
            }

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
            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);

            bool isAdmin = string.Equals(perfilLogado, "admin", StringComparison.OrdinalIgnoreCase);
            bool isOwner = string.Equals(perfilLogado, "owner", StringComparison.OrdinalIgnoreCase);

            if (isAdmin)
            {
                ModelState.Remove("EmpresaId");
            }

            if (string.IsNullOrWhiteSpace(model.Senha))
            {
                ModelState.Remove("Senha");
            }

            if (!ModelState.IsValid)
            {
                CarregarViewBags();
                return View("Manter", model);
            }

            bool emailJaExiste = await _context.Logins.AnyAsync(l => l.Email == model.Email && l.Id != model.Id);
            if (emailJaExiste)
            {
                ViewBag.Erro = "Este e-mail já está sendo utilizado por outro usuário no sistema.";
                CarregarViewBags();
                return View("Manter", model);
            }

            var loginOriginal = await _context.Logins
                .Include(l => l.Funcionario)
                .FirstOrDefaultAsync(x => x.Id == model.Id);

            if (loginOriginal == null) return NotFound();

            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (isAdmin && loginOriginal.EmpresaId != empIdLogado)
            {
                return RedirectToAction("Index", "Home");
            }

            bool nomeJaExisteNaEmpresa = await _context.Logins
                .AnyAsync(l => l.EmpresaId == loginOriginal.EmpresaId
                            && l.Id != model.Id
                            && l.Funcionario != null
                            && l.Funcionario.Nome == model.Nome);

            if (nomeJaExisteNaEmpresa)
            {
                ViewBag.Erro = $"Atenção: Já existe outro colaborador chamado '{model.Nome}' cadastrado na sua empresa.";
                CarregarViewBags();
                return View("Manter", model);
            }

            var claimEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";
            var claimNome = User.Identity?.Name ?? "";

            bool editandoEleMesmo = false;

            if (!string.IsNullOrEmpty(claimEmail) && string.Equals(loginOriginal.Email, claimEmail, StringComparison.OrdinalIgnoreCase))
            {
                editandoEleMesmo = true;
            }
            else if (!string.IsNullOrEmpty(claimNome) && loginOriginal.Funcionario != null && string.Equals(loginOriginal.Funcionario.Nome, claimNome, StringComparison.OrdinalIgnoreCase))
            {
                editandoEleMesmo = true;
            }

            loginOriginal.Email = model.Email;

            if (isAdmin)
            {
                if (!editandoEleMesmo && !string.Equals(model.PerfilAcesso, "owner", StringComparison.OrdinalIgnoreCase))
                {
                    loginOriginal.PerfilAcesso = model.PerfilAcesso;
                }
            }
            else if (isOwner)
            {
                loginOriginal.PerfilAcesso = model.PerfilAcesso;
            }

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

            var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
            var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

            if (perfilLogado == "admin" && login.EmpresaId != empIdLogado)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(login);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id)
        {
            var login = await _context.Logins.Include(l => l.Funcionario).FirstOrDefaultAsync(m => m.Id == id);

            if (login != null)
            {
                var perfilLogado = User.FindFirstValue(ClaimTypes.Role);
                var empIdLogado = int.Parse(User.FindFirstValue("EmpresaId") ?? "0");

               
                var emailLogado = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";

               
                if (!string.IsNullOrEmpty(login.Email) && login.Email.Equals(emailLogado, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Erro"] = "Não é possível excluir a sua própria conta ativa.";
                    return RedirectToAction(nameof(Index));
                }

                
                if (!string.IsNullOrEmpty(login.PerfilAcesso) && login.PerfilAcesso.Equals("owner", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Erro"] = "Contas com perfil de 'Owner' não podem ser excluídas do sistema.";
                    return RedirectToAction(nameof(Index));
                }

                
                if (perfilLogado != null && perfilLogado.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    
                    if (login.EmpresaId != empIdLogado)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    
                    if (login.PerfilAcesso == null || !login.PerfilAcesso.Equals("users", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["Erro"] = "Você não pode excluir um usuário com o mesmo nível de permissão que o seu.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var emailLog = login.Email;
                var empIdLog = login.EmpresaId ?? 0;

                try
                {
                    
                    if (login.Funcionario != null)
                    {
                        _context.Funcionarios.Remove(login.Funcionario);
                    }

                    _context.Logins.Remove(login);

                    await _context.SaveChangesAsync();
                    await RegistrarLog("DELETE", $"Usuário {emailLog} excluído", empIdLog);

                    TempData["Sucesso"] = "Usuário excluído com sucesso!";
                }
                catch (Exception)
                {
                    
                    TempData["Erro"] = "Erro de exclusão: Este usuário possui vínculos importantes no sistema e não pôde ser excluído.";
                }
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