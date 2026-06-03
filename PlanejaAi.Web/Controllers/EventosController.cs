using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;
using System.Text;

namespace PlanejaAi.Controllers
{
    [Authorize]
    public class EventosController : Controller
    {
        private readonly AppDbContext _context;

        public EventosController(AppDbContext context)
        {
            _context = context;
        }

        private int GetEmpresaId()
        {
            var empIdClaim = User.FindFirst("EmpresaId")?.Value;
            return int.TryParse(empIdClaim, out int empId) ? empId : 0;
        }

        private async Task RegistrarLog(string acao, string descricao, int? eventoId = null)
        {
            try
            {
                var usuarioNome = User.Identity?.Name ?? "Sistema";
                var ipClient = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "::1";
                var empId = GetEmpresaId();

                var novoLog = new Log
                {
                    Acao = acao,
                    Tabela = "eventos",
                    Descricao = descricao,
                    Usuario = usuarioNome,
                    Ip = ipClient,
                    Data = DateTime.Now,
                    EmpresaId = empId > 0 ? empId : null,
                    EventoId = eventoId
                };

                _context.Logs.Add(novoLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao registrar log: " + ex.Message);
            }
        }

        private async Task CarregarViewBags(int empId, bool isOwner)
        {
            var clientesQuery = _context.Clientes.AsQueryable();
            if (!isOwner) clientesQuery = clientesQuery.Where(c => c.EmpresaId == empId);
            ViewBag.Clientes = new SelectList(await clientesQuery.OrderBy(c => c.Nome).ToListAsync(), "Id", "Nome");

            var fornecedoresQuery = _context.Fornecedores.AsQueryable();
            if (!isOwner) fornecedoresQuery = fornecedoresQuery.Where(f => f.EmpresaId == empId);

            var fornecedores = await fornecedoresQuery
                .Where(f => f.Status == true)
                .OrderBy(f => f.Nome)
                .ToListAsync();

            ViewBag.Fornecedores = new SelectList(fornecedores, "Id", "Nome");
        }

        public async Task<IActionResult> Index(string busca)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var eventosQuery = _context.Eventos.Include(e => e.Cliente).Include(e => e.Empresa).AsQueryable();

            if (!isOwner) eventosQuery = eventosQuery.Where(e => e.EmpresaId == empId);

            if (!string.IsNullOrEmpty(busca))
            {
                eventosQuery = eventosQuery.Where(e => e.Nome.Contains(busca) || e.Tipo.Contains(busca));
                ViewData["FiltroAtual"] = busca;
            }

            return View(await eventosQuery.OrderBy(e => e.DataEvento).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Manter(int? id)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            if (id == null)
            {
                await CarregarViewBags(empId, isOwner);
                return View(new Evento { DataEvento = DateTime.Now, DataCriacao = DateTime.Now, LocalTipo = 1 });
            }

            var query = _context.Eventos.Include(e => e.ProdutoLocal).AsQueryable();
            if (!isOwner) query = query.Where(e => e.EmpresaId == empId);

            var evento = await query.FirstOrDefaultAsync(e => e.Id == id);
            if (evento == null) return NotFound();

            if (evento.Status == "Concluído" || evento.Status == "Cancelado")
            {
                TempData["Erro"] = "Eventos concluídos ou cancelados não podem ser alterados.";
                return RedirectToAction(nameof(Detalhes), new { id = evento.Id });
            }

            if (evento.LocalTipo == 2 && evento.ProdutoLocal != null)
            {
                ViewBag.FornecedorIdSelecionado = evento.ProdutoLocal.FornecedorId;
                ViewBag.CategoriaIdSelecionada = evento.ProdutoLocal.CategoriaId;
            }

            await CarregarViewBags(empId, isOwner);
            return View(evento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manter(Evento evento)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            if (evento.Id == 0)
            {
                if (evento.EmpresaId == 0) evento.EmpresaId = empId;
                evento.Status = "Em Planejamento";
            }
            else
            {
                var original = await _context.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == evento.Id);
                if (original != null)
                {
                    if (!isOwner && original.EmpresaId != empId) return Unauthorized();

                    if (original.Status == "Concluído" || original.Status == "Cancelado")
                    {
                        TempData["Erro"] = "Eventos concluídos ou cancelados não podem ser alterados.";
                        return RedirectToAction(nameof(Detalhes), new { id = evento.Id });
                    }

                    evento.EmpresaId = original.EmpresaId;
                    evento.DataCriacao = original.DataCriacao;
                    evento.Status = original.Status;
                }
            }


            if (evento.LocalTipo == 1)
            {
                evento.ProdutoLocalId = null;
            }
            else
            {
                evento.NomeLocalProprio = null;
                evento.ValorLocalProprio = 0;
            }

            ModelState.Remove("Empresa");
            ModelState.Remove("Cliente");
            ModelState.Remove("EventoItens");
            ModelState.Remove("ProdutoLocal");

            if (ModelState.IsValid)
            {
                string detalheLocal = "";

                if (evento.LocalTipo == 1 && evento.ValorLocalProprio > 0)
                {
                    detalheLocal = $" Definido Local Próprio: '{evento.NomeLocalProprio}' no valor de R$ {evento.ValorLocalProprio:N2}.";
                }
                else if (evento.LocalTipo == 2 && evento.ProdutoLocalId.HasValue)
                {
                    var produto = await _context.ProdutosFornecedor.AsNoTracking().FirstOrDefaultAsync(p => p.Id == evento.ProdutoLocalId.Value);
                    if (produto != null)
                    {
                        detalheLocal = $" Definido Local Parceiro: '{produto.Nome}' no valor de R$ {produto.ValorPadrao:N2}.";
                    }
                }

                if (evento.Id == 0)
                {
                    _context.Add(evento);
                    await _context.SaveChangesAsync();

                    await RegistrarLog("CREATE", $"Evento {evento.Nome} criado.{detalheLocal}", evento.Id);

                    TempData["Sucesso"] = "Evento criado!";
                }
                else
                {
                    _context.Update(evento);
                    await _context.SaveChangesAsync();

                    await RegistrarLog("UPDATE", $"Configurações do evento {evento.Nome} foram atualizadas.{detalheLocal}", evento.Id);

                    TempData["Sucesso"] = "Evento atualizado!";
                }

                return RedirectToAction(nameof(Index));
            }

            await CarregarViewBags(empId, isOwner);
            return View(evento);
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var query = _context.Eventos
                .Include(e => e.Cliente)
                .Include(e => e.EventoItens)
                .Include(e => e.ProdutoLocal)
                .Include(e => e.Checklists)
                .AsNoTracking();

            if (!isOwner) query = query.Where(e => e.EmpresaId == empId);

            var evento = await query.FirstOrDefaultAsync(m => m.Id == id);
            if (evento == null) return NotFound();

            ViewBag.Logs = await _context.Logs
                .Where(l => l.EventoId == id)
                .OrderByDescending(l => l.Data)
                .ToListAsync();

            if (evento.EventoItens != null && evento.EventoItens.Any())
            {
                var produtoIds = evento.EventoItens
                    .Where(i => i.ProdutoId.HasValue)
                    .Select(i => i.ProdutoId.Value)
                    .Distinct()
                    .ToList();

                var categoriasDosProdutos = await _context.ProdutosFornecedor
                    .Include(p => p.Categoria)
                    .Where(p => produtoIds.Contains(p.Id))
                    .ToDictionaryAsync(
                        p => p.Id,
                        p => p.Categoria != null ? p.Categoria.Nome : "Sem Categoria"
                    );

                foreach (var item in evento.EventoItens)
                {
                    if (item.ProdutoId.HasValue && categoriasDosProdutos.ContainsKey(item.ProdutoId.Value))
                    {
                        item.Categoria = categoriasDosProdutos[item.ProdutoId.Value];
                    }
                }
            }

            return View(evento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner") || User.IsInRole("Owner");

            var query = _context.Eventos.AsQueryable();
            if (!isOwner) query = query.Where(e => e.EmpresaId == empId);

            var evento = await query.FirstOrDefaultAsync(e => e.Id == id);

            if (evento != null)
            {
                var convidadosVinculados = _context.Convidados.Where(c => c.EventoId == id);

                _context.Convidados.RemoveRange(convidadosVinculados);

                _context.Eventos.Remove(evento);

                await _context.SaveChangesAsync();

                await RegistrarLog("DELETE", $"Evento {evento.Nome} e seus convidados foram removidos completamente do sistema.");

                TempData["Sucesso"] = "Evento excluído com sucesso!";
            }
            else
            {
                TempData["Erro"] = "Evento não encontrado ou você não tem permissão para excluí-lo.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> BuscarCategoriasPorFornecedor(int fornecedorId)
        {
            var categorias = await _context.ProdutosFornecedor
                .Include(p => p.Categoria)
                .Where(p => p.FornecedorId == fornecedorId && p.Ativo)
                .Select(p => new { id = p.CategoriaId, nome = p.Categoria.Nome })
                .Distinct()
                .ToListAsync();

            return Json(categorias);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> BuscarLocaisPorCategoria(int fornecedorId, int categoriaId)
        {
            var produtos = await _context.ProdutosFornecedor
                .Where(p => p.FornecedorId == fornecedorId && p.CategoriaId == categoriaId && p.Ativo)
                .Select(p => new { id = p.Id, nome = p.Nome, custo = p.ValorPadrao })
                .ToListAsync();

            return Json(produtos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarStatus(int id, string novoStatus)
        {

            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null) return NotFound();

            int empId = GetEmpresaId();
            if (!User.IsInRole("owner") && evento.EmpresaId != empId)
            {
                TempData["Erro"] = "Você não tem permissão para alterar este evento.";
                return RedirectToAction(nameof(Index));
            }

            evento.Status = novoStatus;

            _context.Eventos.Update(evento);
            await _context.SaveChangesAsync();
            await RegistrarLog("STATUS", $"O status do evento foi alterado para '{novoStatus}'.", evento.Id);

            TempData["Sucesso"] = $"O evento foi marcado como {novoStatus}.";

            return RedirectToAction(nameof(Detalhes), new { id = evento.Id });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarChecklist(int EventoId, string Descricao)
        {
            if (string.IsNullOrWhiteSpace(Descricao))
            {
                TempData["Erro"] = "A descrição da tarefa não pode estar vazia.";
                return RedirectToAction(nameof(Detalhes), new { id = EventoId });
            }

            var checklist = new Checklist
            {
                EventoId = EventoId,
                Descricao = Descricao,
                Concluido = false
            };

            _context.Checklists.Add(checklist);
            await _context.SaveChangesAsync();

            await RegistrarLog("CHECKLIST", $"Nova tarefa adicionada: '{Descricao}'", EventoId);

            TempData["AbrirChecklist"] = true;
            return RedirectToAction(nameof(Detalhes), new { id = EventoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletarChecklist(int id, int eventoId)
        {
            var item = await _context.Checklists.FindAsync(id);
            if (item != null)
            {
                string desc = item.Descricao;
                _context.Checklists.Remove(item);
                await _context.SaveChangesAsync();

                await RegistrarLog("CHECKLIST", $"Tarefa removida: '{desc}'", eventoId);
                TempData["AbrirChecklist"] = true;
            }
            return RedirectToAction(nameof(Detalhes), new { id = eventoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarStatusChecklist(int id, bool concluido)
        {
            try
            {
                var tarefa = await _context.Checklists.FindAsync(id);

                if (tarefa == null)
                {
                    return NotFound();
                }

                tarefa.Concluido = concluido;

                _context.Update(tarefa);
                await _context.SaveChangesAsync();

                string status = concluido ? "concluída" : "desmarcada";
                await RegistrarLog("CHECKLIST", $"Tarefa '{tarefa.Descricao}' marcada como {status}.", tarefa.EventoId);

                return Ok();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro AJAX Checklist: " + ex.Message);
                return StatusCode(500, "Erro ao atualizar a tarefa.");
            }
        }

        [Authorize]
        public IActionResult ListaConvidados(int id)
        {
            var evento = _context.Eventos.FirstOrDefault(e => e.Id == id);
            if (evento == null) return NotFound();

            var convidados = _context.Convidados
                                     .Where(c => c.EventoId == id)
                                     .OrderByDescending(c => c.Confirmacao)
                                     .ThenBy(c => c.Nome)
                                     .ToList();

            ViewBag.EventoNome = evento.Nome;
            ViewBag.EventoId = id;
            ViewBag.EventoStatus = evento.Status;

            return View(convidados);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarConvidadoManual(int eventoId, string nome, int status)
        {
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var novoConvidado = new Convidado
                {
                    EventoId = eventoId,
                    Nome = nome,
                    Confirmacao = status,
                    DataCadastro = DateTime.Now
                };

                _context.Convidados.Add(novoConvidado);
                await _context.SaveChangesAsync();
                await RegistrarLog("CREATE", $"Convidado '{nome}' adicionado manualmente.", eventoId);

                TempData["Sucesso"] = "Convidado adicionado com sucesso!";
            }

            return RedirectToAction("ListaConvidados", new { id = eventoId });
        }

        [HttpGet]
        [Authorize]
        public IActionResult EditarConvidado(int id)
        {
            var convidado = _context.Convidados.Find(id);
            if (convidado == null) return NotFound();

            var evento = _context.Eventos.Find(convidado.EventoId);
            ViewBag.EventoNome = evento?.Nome ?? "Evento Desconhecido";

            return View(convidado);
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarConvidado(int id, int eventoId, string nome, int status)
        {
            var convidado = await _context.Convidados.FindAsync(id);

            if (convidado != null && !string.IsNullOrWhiteSpace(nome))
            {
                convidado.Nome = nome;
                convidado.Confirmacao = status;

                _context.Convidados.Update(convidado);
                await _context.SaveChangesAsync();

                await RegistrarLog("UPDATE", $"Convidado ID {id} ('{nome}') atualizado.", eventoId);

                TempData["Sucesso"] = "Dados do convidado atualizados!";
            }

            return RedirectToAction("ListaConvidados", new { id = eventoId });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Inscricao(int id)
        {
            var evento = _context.Eventos.FirstOrDefault(e => e.Id == id);
            if (evento == null) return NotFound();

            ViewBag.EventoNome = evento.Nome;
            ViewBag.EventoId = id;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPresenca(Convidado model)
        {
            if (string.IsNullOrWhiteSpace(model.Nome))
            {
                return RedirectToAction("Inscricao", new { id = model.EventoId });
            }

            model.DataCadastro = DateTime.Now;

            _context.Convidados.Add(model);
            await _context.SaveChangesAsync();

            await RegistrarLog("CREATE", $"Inscrição via link público recebida para '{model.Nome}'.", model.EventoId);

            TempData["SucessoInscricao"] = "Sua resposta foi enviada com sucesso. Obrigado!";

            return RedirectToAction("Inscricao", new { id = model.EventoId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverConvidado(int id, int eventoId)
        {
            var convidado = await _context.Convidados.FindAsync(id);

            if (convidado != null)
            {
                string nomeExcluido = convidado.Nome;

                _context.Convidados.Remove(convidado);
                await _context.SaveChangesAsync();

                await RegistrarLog("DELETE", $"Convidado ID {id} ('{nomeExcluido}') removido da lista.", eventoId);

                TempData["Sucesso"] = "Convidado removido com sucesso!";
            }

            return RedirectToAction("ListaConvidados", new { id = eventoId });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ExportarConvidadosCsv(int eventoId)
        {
            try
            {
                var evento = await _context.Eventos.FindAsync(eventoId);
                if (evento == null) return NotFound();

                var convidados = await _context.Convidados
                                               .Where(c => c.EventoId == eventoId)
                                               .OrderBy(c => c.Nome)
                                               .ToListAsync();

                var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
                bool isOwner = role == "owner";

                var builder = new StringBuilder();

                if (isOwner)
                {
                    builder.AppendLine("Empresa ID;Nome;Status;DataCadastro");
                }
                else
                {
                    builder.AppendLine("Nome;Status;DataCadastro");
                }

                foreach (var c in convidados)
                {
                    string statusTxt = c.Confirmacao switch
                    {
                        1 => "Confirmado",
                        2 => "Em Dúvida",
                        _ => "Não Confirmado"
                    };

                    if (isOwner)
                    {
                        builder.AppendLine($"{evento.EmpresaId};{c.Nome};{statusTxt};{c.DataCadastro:dd/MM/yyyy HH:mm}");
                    }
                    else
                    {
                        builder.AppendLine($"{c.Nome};{statusTxt};{c.DataCadastro:dd/MM/yyyy HH:mm}");
                    }
                }

                await RegistrarLog("EXPORT", $"Lista de convidados exportada (Total: {convidados.Count} registros).", eventoId);

                var utf8ComBom = new UTF8Encoding(true);
                byte[] preamble = utf8ComBom.GetPreamble();
                byte[] content = utf8ComBom.GetBytes(builder.ToString());
                byte[] finalBytes = preamble.Concat(content).ToArray();

                return File(finalBytes, "text/csv", $"Convidados_{evento.Nome.Replace(" ", "_")}.csv");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao exportar arquivo: {ex.Message}";
                return RedirectToAction(nameof(ListaConvidados), new { id = eventoId });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarConvidadosCsv(IFormFile arquivoCsv, int eventoId)
        {
            if (arquivoCsv == null || arquivoCsv.Length == 0)
            {
                TempData["Erro"] = "Por favor, selecione um arquivo CSV válido.";
                return RedirectToAction(nameof(ListaConvidados), new { id = eventoId });
            }

            try
            {
                int totalInserido = 0;
                int totalAtualizado = 0;
                int numeroDaLinha = 1;

                var convidadosExistentes = await _context.Convidados
                    .Where(c => c.EventoId == eventoId)
                    .ToListAsync();

                using (var stream = new StreamReader(arquivoCsv.OpenReadStream(), Encoding.UTF8))
                {
                    var cabecalho = await stream.ReadLineAsync();

                    if (cabecalho != null && (cabecalho.ToLower().Contains("cnpj") || cabecalho.ToLower().Contains("cpf") || cabecalho.ToLower().Contains("fornecedor")))
                    {
                        TempData["Erro"] = "Importação cancelada: O layout do arquivo enviado é incompatível com o modelo esperado para esta tela.";
                        return RedirectToAction(nameof(ListaConvidados), new { id = eventoId });
                    }

                    while (!stream.EndOfStream)
                    {
                        var linha = await stream.ReadLineAsync();
                        numeroDaLinha++;

                        if (string.IsNullOrWhiteSpace(linha)) continue;

                        var colunas = linha.Split(';');

                        var nome = colunas.Length > 0 ? colunas[0]?.Trim() : string.Empty;
                        var statusTexto = colunas.Length > 1 ? colunas[1]?.Trim().Replace("\"", "") : string.Empty;

                        if (string.IsNullOrEmpty(nome))
                        {
                            TempData["Erro"] = $"Erro na linha {numeroDaLinha}: Existem campos obrigatórios vazios. Certifique-se de preencher todas as informações necessárias na planilha.";
                            return RedirectToAction(nameof(ListaConvidados), new { id = eventoId });
                        }

                        if (!string.IsNullOrEmpty(statusTexto) && System.Text.RegularExpressions.Regex.IsMatch(statusTexto, @"\d"))
                        {
                            TempData["Erro"] = $"Erro na linha {numeroDaLinha}: O formato dos dados é inválido para este tipo de cadastro. Certifique-se de que selecionou o arquivo correto.";
                            return RedirectToAction(nameof(ListaConvidados), new { id = eventoId });
                        }

                        int statusId = 0;
                        var statusLower = statusTexto.ToLower();

                        if (statusLower.Contains("confirmado") && !statusLower.Contains("não") && !statusLower.Contains("nao"))
                            statusId = 1;
                        else if (statusLower.Contains("dúvida") || statusLower.Contains("duvida"))
                            statusId = 2;

                        var convidadoAtual = convidadosExistentes.FirstOrDefault(c => c.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

                        if (convidadoAtual != null)
                        {
                            convidadoAtual.Confirmacao = statusId;
                            _context.Convidados.Update(convidadoAtual);
                            totalAtualizado++;
                        }
                        else
                        {
                            _context.Convidados.Add(new Convidado
                            {
                                EventoId = eventoId,
                                Nome = nome,
                                Confirmacao = statusId,
                                DataCadastro = DateTime.Now
                            });
                            totalInserido++;
                        }
                    }
                }

                if (totalInserido > 0 || totalAtualizado > 0)
                {
                    await _context.SaveChangesAsync();
                    await RegistrarLog("CREATE", $"Importação CSV: {totalInserido} inseridos, {totalAtualizado} atualizados.", eventoId);
                    TempData["Sucesso"] = $"Importação finalizada com sucesso! {totalInserido} novos convidados adicionados e {totalAtualizado} atualizados.";
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

            return RedirectToAction(nameof(ListaConvidados), new { id = eventoId });
        }

        [HttpGet]
        [Authorize(Roles = "owner,admin")]
        public async Task<IActionResult> ExportarCsv()
        {
            var role = User.FindFirstValue(ClaimTypes.Role)?.ToLower();
            int empIdLogado = GetEmpresaId();
            bool isOwner = role == "owner";

            if (role != "admin" && role != "owner")
            {
                TempData["Erro"] = "Acesso negado. Apenas Administradores podem exportar a base de eventos.";
                return RedirectToAction(nameof(Index));
            }

            var query = _context.Eventos
                .Include(e => e.Cliente)
                .Include(e => e.ProdutoLocal)
                .Include(e => e.EventoItens)
                .AsNoTracking()
                .AsQueryable();

            if (!isOwner)
            {
                query = query.Where(e => e.EmpresaId == empIdLogado);
            }

            var eventos = await query.OrderBy(e => e.DataEvento).ToListAsync();

            if (!eventos.Any())
            {
                TempData["Erro"] = "Nenhum evento encontrado para exportação.";
                return RedirectToAction(nameof(Index));
            }

            var csv = new StringBuilder();

            if (isOwner)
            {
                csv.AppendLine("Empresa ID;Nome do Evento;Tipo;Status;Data do Evento;Cliente;Tipo de Local;Nome do Local;Custo do Local;Valor Total Orçamento;Total de Gastos;Saldo Disponível;Qtd Convidados;Privacidade;Data de Criação");
            }
            else
            {
                csv.AppendLine("Nome do Evento;Tipo;Status;Data do Evento;Cliente;Tipo de Local;Nome do Local;Custo do Local;Valor Total Orçamento;Total de Gastos;Saldo Disponível;Qtd Convidados;Privacidade;Data de Criação");
            }

            foreach (var e in eventos)
            {
                string nome = e.Nome?.Replace(";", ",") ?? "";
                string tipo = e.Tipo?.Replace(";", ",") ?? "-";
                string status = e.Status ?? "-";
                string dataEvento = e.DataEvento.ToString("dd/MM/yyyy HH:mm");
                string cliente = e.Cliente?.Nome?.Replace(";", ",") ?? "Sem Cliente";

                string tipoLocal = "";
                string nomeLocal = "";
                decimal custoLocalDecimal = 0;

                if (e.LocalTipo == 1)
                {
                    tipoLocal = "Próprio";
                    nomeLocal = e.NomeLocalProprio?.Replace(";", ",") ?? "Não informado";
                    custoLocalDecimal = e.ValorLocalProprio;
                }
                else if (e.LocalTipo == 2)
                {
                    tipoLocal = "Parceiro/Fornecedor";
                    nomeLocal = e.ProdutoLocal?.Nome?.Replace(";", ",") ?? "Fornecedor/Local não encontrado";
                    custoLocalDecimal = e.ProdutoLocal?.ValorPadrao ?? 0;
                }
                else
                {
                    tipoLocal = "Não definido";
                    nomeLocal = "-";
                }

                decimal valorOrcamentoDecimal = e.ValorTotalOrcamento;
                decimal totalItensDecimal = e.EventoItens?.Sum(i => i.Valor) ?? 0;

                decimal totalGastosDecimal = custoLocalDecimal + totalItensDecimal;
                decimal saldoDisponivelDecimal = valorOrcamentoDecimal - totalGastosDecimal;

                string custoLocal = $"=\"{custoLocalDecimal.ToString("F2", new System.Globalization.CultureInfo("pt-BR"))}\"";
                string valorOrcamento = $"=\"{valorOrcamentoDecimal.ToString("F2", new System.Globalization.CultureInfo("pt-BR"))}\"";
                string totalGastos = $"=\"{totalGastosDecimal.ToString("F2", new System.Globalization.CultureInfo("pt-BR"))}\"";
                string saldoDisponivel = $"=\"{saldoDisponivelDecimal.ToString("F2", new System.Globalization.CultureInfo("pt-BR"))}\"";

                string qtdConvidados = e.NumeroConvidados?.ToString() ?? "0";
                string privacidade = e.Privacidade?.Replace(";", ",") ?? "-";
                string dataCriacao = e.DataCriacao.ToString("dd/MM/yyyy HH:mm");

                if (isOwner)
                {
                    csv.AppendLine($"{e.EmpresaId};{nome};{tipo};{status};{dataEvento};{cliente};{tipoLocal};{nomeLocal};{custoLocal};{valorOrcamento};{totalGastos};{saldoDisponivel};{qtdConvidados};{privacidade};{dataCriacao}");
                }
                else
                {
                    csv.AppendLine($"{nome};{tipo};{status};{dataEvento};{cliente};{tipoLocal};{nomeLocal};{custoLocal};{valorOrcamento};{totalGastos};{saldoDisponivel};{qtdConvidados};{privacidade};{dataCriacao}");
                }
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = preamble.Concat(contentBytes).ToArray();

            string nomeArquivo = $"Relatorio_Eventos_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            await RegistrarLog("EXPORT", $"Exportação de {eventos.Count} eventos realizada com sucesso.");

            return File(bytes, "text/csv", nomeArquivo);
        }
    }
}