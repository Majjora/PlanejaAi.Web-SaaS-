using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MySqlConnector;
using System.Net;
using System.Net.Mail;
using System;
using System.Linq;
using PlanejaAi.Data;
using PlanejaAi.Models;

namespace PlanejaAi.Controllers
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class LoginController : Controller
    {
        private readonly string stringConexao = "Server=localhost;Database=planeja_ai;User=root;Password=felps123;";
        private readonly AppDbContext _context;

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Entrar(string email, string senha)
        {
            string ipAtual = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";

            using (MySqlConnection conexao = new MySqlConnection(stringConexao))
            {
                await conexao.OpenAsync();


                string sql = "SELECT f.func_id, f.func_nome, l.perfil_acesso, l.emp_id, l.login_senha, l.login_senha_atualizacao FROM login l INNER JOIN funcionarios f ON l.func_id = f.func_id WHERE l.login_email = @email";

                using (MySqlCommand comando = new MySqlCommand(sql, conexao))
                {
                    comando.Parameters.AddWithValue("@email", email);

                    using (MySqlDataReader leitor = await comando.ExecuteReaderAsync())
                    {
                        if (await leitor.ReadAsync())
                        {
                            string hashSalvoNoBanco = leitor.GetString("login_senha");
                            bool senhaCorreta = false;

                            try { senhaCorreta = BCrypt.Net.BCrypt.Verify(senha, hashSalvoNoBanco); }
                            catch { senhaCorreta = false; }

                            if (senhaCorreta)
                            {
                                string nomeFuncionario = leitor.GetString("func_nome");
                                string perfilAcesso = leitor.GetString("perfil_acesso");
                                string empresaId = leitor["emp_id"].ToString();
                                string funcId = leitor["func_id"].ToString();

                                
                                DateTime? dataAtualizacao = leitor.IsDBNull(leitor.GetOrdinal("login_senha_atualizacao"))
                                    ? (DateTime?)null
                                    : leitor.GetDateTime("login_senha_atualizacao");

                                if (dataAtualizacao == null || dataAtualizacao.Value.AddDays(30) < DateTime.Now)
                                {
                                    leitor.Close(); 
                                    string novoToken = Guid.NewGuid().ToString();
                                    string sqlToken = "UPDATE login SET login_token = @token, login_token_expiracao = @expiracao WHERE login_email = @email";

                                    using (MySqlCommand cmdT = new MySqlCommand(sqlToken, conexao))
                                    {
                                        cmdT.Parameters.AddWithValue("@token", novoToken);
                                        cmdT.Parameters.AddWithValue("@expiracao", DateTime.Now.AddMinutes(10));
                                        cmdT.Parameters.AddWithValue("@email", email);
                                        await cmdT.ExecuteNonQueryAsync();
                                    }

                                   
                                    if (dataAtualizacao == null)
                                    {
                                        TempData["AvisoNovaSenha"] = "Este é o seu primeiro acesso. Por favor, crie uma senha definitiva por segurança.";
                                    }
                                    else
                                    {
                                        TempData["AvisoNovaSenha"] = "Sua senha expirou por segurança (mais de 30 dias). Por favor, crie uma nova senha.";
                                    }

                                    return RedirectToAction("NovaSenha", new { token = novoToken });
                                }

                                var claims = new List<Claim> {
                                    new Claim(ClaimTypes.NameIdentifier, funcId),
                                    new Claim(ClaimTypes.Name, nomeFuncionario),
                                    new Claim("Nome", nomeFuncionario),
                                    new Claim(ClaimTypes.Role, perfilAcesso),
                                    new Claim("EmpresaId", empresaId)
                                };

                                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                                await SalvarLog("LOGIN", "login", "Acesso realizado com sucesso", int.Parse(empresaId), nomeFuncionario, ipAtual);

                                return RedirectToAction("Index", "Home");
                            }
                            else
                            {
                                await SalvarLog("LOGIN_FALHA", "login", "Tentativa de acesso negada: senha incorreta", 0, email, ipAtual);
                            }
                        }
                        else
                        {
                            await SalvarLog("LOGIN_FALHA", "login", "Tentativa de acesso negada: usuário não encontrado", 0, email, ipAtual);
                        }
                    }
                }
            }
            ViewBag.Erro = "E-mail ou senha inválidos!";
            return View("Index");
        }

        [HttpGet]
        public IActionResult RecuperarSenha() => View();

        [HttpPost]
        public async Task<IActionResult> RecuperarSenha(string email)
        {
            string ipAtual = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";
            string token = Guid.NewGuid().ToString();

            using (MySqlConnection conexao = new MySqlConnection(stringConexao))
            {
                await conexao.OpenAsync();
                string sqlBusca = "SELECT l.emp_id, f.func_nome FROM login l INNER JOIN funcionarios f ON l.func_id = f.func_id WHERE l.login_email = @email";

                using (MySqlCommand cmd = new MySqlCommand(sqlBusca, conexao))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    using (var leitor = await cmd.ExecuteReaderAsync())
                    {
                        if (await leitor.ReadAsync())
                        {
                            int empId = leitor.GetInt32("emp_id");
                            string nomeUsuario = leitor.GetString("func_nome");
                            leitor.Close();

                            
                            string sqlToken = "UPDATE login SET login_token = @token, login_token_expiracao = @expiracao WHERE login_email = @email";
                            using (MySqlCommand cmdT = new MySqlCommand(sqlToken, conexao))
                            {
                                cmdT.Parameters.AddWithValue("@token", token);
                                cmdT.Parameters.AddWithValue("@expiracao", DateTime.Now.AddMinutes(10));
                                cmdT.Parameters.AddWithValue("@email", email);
                                await cmdT.ExecuteNonQueryAsync();
                            }

                            string link = $"https://localhost:7094/Login/NovaSenha?token={token}";
                            EnviarEmailComLink(email, nomeUsuario, link);

                            await SalvarLog("RECUPERAR", "login", "Link de recuperação enviado", empId, email, ipAtual);

                            ViewBag.Mensagem = "Link enviado com sucesso! Verifique seu e-mail.";
                            return View();
                        }
                    }
                }
            }

            await SalvarLog("RECUPERAR_FALHA", "login", $"Tentativa de recuperação: e-mail {email} não encontrado", 0, email, ipAtual);
            ViewBag.Erro = "E-mail não encontrado.";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> NovaSenha(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Index");

            using (MySqlConnection conexao = new MySqlConnection(stringConexao))
            {
                await conexao.OpenAsync();
                
                string sql = "SELECT COUNT(*) FROM login WHERE login_token = @token AND login_token IS NOT NULL AND login_token_expiracao > NOW()";
                using (MySqlCommand cmd = new MySqlCommand(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@token", token);
                    var existe = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (existe == 0)
                    {
                        TempData["Erro"] = "Este link já foi utilizado ou expirou o prazo de 10 minutos.";
                        return RedirectToAction("Index");
                    }
                }
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> NovaSenha(string token, string novaSenha)
        {
            string ipAtual = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";

            bool requisitosPreenchidos = !string.IsNullOrEmpty(novaSenha) &&
                                         novaSenha.Length >= 8 &&
                                         novaSenha.Any(char.IsUpper) &&
                                         novaSenha.Any(char.IsDigit) &&
                                         novaSenha.Any(ch => !char.IsLetterOrDigit(ch));

            if (!requisitosPreenchidos)
            {
                ViewBag.Erro = "A senha deve conter no mínimo 8 caracteres, incluindo letras maiúsculas, números e caracteres especiais.";
                ViewBag.Token = token;
                return View();
            }

            using (MySqlConnection conexao = new MySqlConnection(stringConexao))
            {
                await conexao.OpenAsync();

                
                string sqlBusca = "SELECT emp_id, login_email, login_senha FROM login WHERE login_token = @token AND login_token_expiracao > NOW()";
                int empId = 0;
                string usuarioEmail = "";
                string senhaAtual = "";

                using (MySqlCommand cmdBusca = new MySqlCommand(sqlBusca, conexao))
                {
                    cmdBusca.Parameters.AddWithValue("@token", token);
                    using (var leitor = await cmdBusca.ExecuteReaderAsync())
                    {
                        if (await leitor.ReadAsync())
                        {
                            empId = Convert.ToInt32(leitor["emp_id"]);
                            usuarioEmail = leitor["login_email"].ToString();
                            senhaAtual = leitor["login_senha"].ToString();
                        }
                        else
                        {
                            TempData["Erro"] = "Sessão expirada. Solicite a redefinição de senha novamente.";
                            return RedirectToAction("Index");
                        }
                    }
                }

                bool senhaJaEraUsada = false;
                try { senhaJaEraUsada = BCrypt.Net.BCrypt.Verify(novaSenha, senhaAtual); }
                catch { senhaJaEraUsada = (novaSenha == senhaAtual); }

                if (senhaJaEraUsada)
                {
                    ViewBag.Erro = "A nova senha não pode ser igual à senha utilizada anteriormente.";
                    ViewBag.Token = token;
                    return View();
                }

                string senhaCriptografada = BCrypt.Net.BCrypt.HashPassword(novaSenha);

               
                string sqlUpdate = "UPDATE login SET login_senha = @senha, login_token = NULL, login_token_expiracao = NULL, login_senha_atualizacao = NOW() WHERE login_token = @token";
                using (MySqlCommand cmdUpdate = new MySqlCommand(sqlUpdate, conexao))
                {
                    cmdUpdate.Parameters.AddWithValue("@senha", senhaCriptografada);
                    cmdUpdate.Parameters.AddWithValue("@token", token);


                    if (await cmdUpdate.ExecuteNonQueryAsync() > 0)
                    {

                        await SalvarLog("ALTERAR_SENHA", "login", "Senha alterada com sucesso via recuperação", empId, usuarioEmail, ipAtual);
                        TempData["Sucesso"] = "Sua senha foi redefinida com sucesso. Acesse sua conta.";
                        return RedirectToAction("Index");
                    }
                }
            }

            ViewBag.Erro = "Ocorreu um erro inesperado.";
            ViewBag.Token = token;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Sair()
        {
            string ipAtual = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";
            var empIdClaim = User.FindFirst("EmpresaId")?.Value;
            int empId = string.IsNullOrEmpty(empIdClaim) ? 0 : int.Parse(empIdClaim);

            await SalvarLog("LOGOUT", "login", "Saída do sistema", empId, User.Identity.Name, ipAtual);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }

        private async Task SalvarLog(string acao, string tabela, string desc, int empId, string usuario, string ip = "IP Desconhecido")
        {
            try
            {
                var novoLog = new Log
                {
                    Acao = acao,
                    Tabela = tabela,
                    Descricao = desc,
                    Usuario = usuario ?? "Sistema",
                    Ip = ip,
                    Data = DateTime.Now,
                    EmpresaId = empId == 0 ? null : empId
                };

                _context.Logs.Add(novoLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERRO LOG EF: " + ex.Message);
            }
        }

        private void EnviarEmailComLink(string emailDestino, string nomeUsuario, string linkRedefinicao)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("felps.curreia@gmail.com", "bojvskuosxlhngtb"),
                    EnableSsl = true,
                };

                string corpoHtml = $@"
<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px; color: #213448;'>
    <h2 style='color: #547792;'>Recuperação de Senha</h2>
    <p>Olá <strong>{nomeUsuario}</strong>,</p>
    <p>Recebemos uma solicitação para redefinir a senha da sua conta no <strong>Planeja Aí</strong>. Clique no botão abaixo para criar uma nova credencial (Este link é válido por 10 minutos):</p>
    
    <div style='margin: 30px 0;'>
        <a href='{linkRedefinicao}' style='background: #547792; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Redefinir Minha Senha</a>
    </div>

    <p style='font-size: 0.85rem; color: #666; margin-top: 20px; line-height: 1.5;'>
        Se o botão acima não funcionar, copie e cole o link abaixo no seu navegador de internet:
        <br><br>
        <a href='{linkRedefinicao}' style='color: #547792; word-break: break-all;'>{linkRedefinicao}</a>
    </p>
</div>";

                var mailMessage = new MailMessage { From = new MailAddress("felps.curreia@gmail.com", "Planeja Aí"), Subject = "Recuperação de Senha", Body = corpoHtml, IsBodyHtml = true };
                mailMessage.To.Add(emailDestino);
                smtpClient.Send(mailMessage);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("ERRO SMTP: " + ex.Message); }
        }
    }
}