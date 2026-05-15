using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;
using PlanejaAi.Models;
using System.Security.Claims;

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
            bool isOwner = User.IsInRole("owner");

            var query = _context.Eventos.AsQueryable();
            if (!isOwner) query = query.Where(e => e.EmpresaId == empId);

            var evento = await query.FirstOrDefaultAsync(e => e.Id == id);

            if (evento != null)
            {
                _context.Eventos.Remove(evento);
                await _context.SaveChangesAsync();

                await RegistrarLog("DELETE", $"Evento {evento.Nome} removido completamente do sistema.");

                TempData["Sucesso"] = "Evento excluído!";
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

    }
}