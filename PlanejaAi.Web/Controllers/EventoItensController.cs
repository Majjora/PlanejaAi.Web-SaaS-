using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Models;
using PlanejaAi.Data;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace PlanejaAi.Controllers
{
    [Authorize]
    public class EventoItensController : Controller
    {
        private readonly AppDbContext _context;

        public EventoItensController(AppDbContext context)
        {
            _context = context;
        }

        private int GetEmpresaId()
        {
            var claim = User.FindFirst("EmpresaId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }
        private async Task CarregarViewBagsFormulario(int? produtoSelecionado = null)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var query = _context.ProdutosFornecedor
                .Include(p => p.Fornecedor)
                .Include(p => p.Categoria)
                .AsQueryable();

            if (!isOwner)
            {
                query = query.Where(p => p.Fornecedor != null && p.Fornecedor.EmpresaId == empId);
            }

            var produtosCompletos = await query.Select(p => new
            {
                Id = p.Id,
                CategoriaId = p.CategoriaId,
                NomeProduto = p.Nome ?? "Produto sem nome",
                NomeFornecedor = p.Fornecedor != null ? p.Fornecedor.Nome : "Sem fornecedor",
                PrecoCusto = p.ValorPadrao ?? 0m,
                PrecoVenda = p.ValorPadrao ?? 0m,
                Unidade = p.Unidade ?? "Unidade"
            }).ToListAsync();

            ViewBag.ProdutosCompletos = produtosCompletos;

            var categoriasIdsUsadas = produtosCompletos.Select(p => p.CategoriaId).Distinct().ToList();

            var categorias = await _context.CategoriasServico
                .Where(c => categoriasIdsUsadas.Contains(c.Id))
                .ToListAsync();

            int? categoriaSelecionada = null;
            if (produtoSelecionado != null)
            {
                var produtoDaEdicao = produtosCompletos.FirstOrDefault(p => p.Id == produtoSelecionado);
                if (produtoDaEdicao != null)
                {
                    categoriaSelecionada = produtoDaEdicao.CategoriaId;
                }
            }

            ViewBag.CategoriasList = new SelectList(categorias, "Id", "Nome", categoriaSelecionada);

            var fallbackList = produtosCompletos.Select(p => new
            {
                Id = p.Id,
                Descricao = $"{p.NomeProduto} ({p.NomeFornecedor})"
            }).ToList();
            ViewBag.ProdutosList = new SelectList(fallbackList, "Id", "Descricao", produtoSelecionado);
        }

        [HttpGet]
        public async Task<IActionResult> Manter(int? id, int eventoId)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var evento = await _context.Eventos
                .Include(e => e.ProdutoLocal)
                .Include(e => e.EventoItens)
                .FirstOrDefaultAsync(e => e.Id == eventoId && (isOwner || e.EmpresaId == empId));

            if (evento == null)
            {
                TempData["Erro"] = "Evento não encontrado ou acesso negado.";
                return RedirectToAction("Index", "Eventos");
            }

            ViewBag.EventoNome = evento.Nome;

            decimal custoLocal = 0m;
            if (evento.LocalTipo == 1)
            {
                custoLocal = evento.ValorLocalProprio;
            }
            else if (evento.LocalTipo == 2 && evento.ProdutoLocal != null)
            {
                custoLocal = evento.ProdutoLocal.ValorPadrao ?? 0m;
            }

            decimal custoItens = evento.EventoItens.Where(i => i.Status != "Cancelado").Sum(i => i.Valor);

            ViewBag.OrcamentoRestante = evento.ValorTotalOrcamento - custoLocal - custoItens;

            if (id == null || id == 0)
            {
                await CarregarViewBagsFormulario();
                return View(new EventoItem { EventoId = eventoId });
            }

            var item = await _context.EventoItens
                .FirstOrDefaultAsync(m => m.Id == id && m.EventoId == eventoId);

            if (item == null) return NotFound();

            await CarregarViewBagsFormulario(item.ProdutoId);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manter(EventoItem item)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");

            var valorForm = Request.Form["Valor"].ToString();
            if (!string.IsNullOrEmpty(valorForm))
            {
                valorForm = valorForm.Replace(".", "").Replace(",", ".");
                if (decimal.TryParse(valorForm, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valorParseado))
                {
                    item.ValorVenda = valorParseado;
                    ModelState.Remove("Valor");
                }
            }

            var evento = await _context.Eventos.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == item.EventoId && (isOwner || e.EmpresaId == empId));

            if (evento == null)
            {
                TempData["Erro"] = "Acesso negado ao evento principal.";
                return RedirectToAction("Index", "Eventos");
            }

            if (ModelState.IsValid)
            {
                string ipUsuario = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";
                string nomeUsuario = User.Identity?.Name ?? "Usuário não identificado";

                if (item.Id == 0)
                {
                    _context.Add(item);

                    var log = new Log
                    {
                        Acao = "CREATE",
                        Tabela = "EventoItem",
                        Descricao = $"Item '{item.Descricao}' adicionado ao evento com status '{item.Status}'. Valor: {item.Valor:C}.",
                        Usuario = nomeUsuario,
                        Ip = ipUsuario,
                        Data = DateTime.Now,
                        EmpresaId = empId,
                        EventoId = item.EventoId
                    };
                    _context.Logs.Add(log);

                    TempData["Sucesso"] = "Item/Serviço adicionado com sucesso!";
                }
                else
                {
                    _context.Update(item);

                    var log = new Log
                    {
                        Acao = "UPDATE",
                        Tabela = "EventoItem",
                        Descricao = $"Item '{item.Descricao}' foi editado. Status: '{item.Status}'. Valor: {item.Valor:C}.",
                        Usuario = nomeUsuario,
                        Ip = ipUsuario,
                        Data = DateTime.Now,
                        EmpresaId = empId,
                        EventoId = item.EventoId
                    };
                    _context.Logs.Add(log);

                    TempData["Sucesso"] = "Item atualizado com sucesso!";
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("Detalhes", "Eventos", new { id = item.EventoId });
            }

            ViewBag.EventoNome = evento.Nome;
            TempData["Erro"] = "Preencha os campos corretamente.";

            await CarregarViewBagsFormulario(item.ProdutoId);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deletar(int id, int eventoId)
        {
            int empId = GetEmpresaId();
            bool isOwner = User.IsInRole("owner");
            bool isAdmin = User.IsInRole("admin");

            if (!isOwner && !isAdmin)
            {
                TempData["Erro"] = "Você não tem permissão para excluir itens deste evento. Solicite a um administrador.";
                return RedirectToAction("Detalhes", "Eventos", new { id = eventoId });
            }

            var evento = await _context.Eventos
                .FirstOrDefaultAsync(e => e.Id == eventoId && (isOwner || e.EmpresaId == empId));

            if (evento == null) return RedirectToAction("Index", "Eventos");

            var item = await _context.EventoItens
                .FirstOrDefaultAsync(m => m.Id == id && m.EventoId == eventoId);

            if (item != null)
            {
                _context.EventoItens.Remove(item);

                string ipUsuario = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";
                string nomeUsuario = User.Identity?.Name ?? "Usuário não identificado";

                var log = new Log
                {
                    Acao = "DELETE",
                    Tabela = "EventoItem",
                    Descricao = $"Item '{item.Descricao}' foi excluído. O valor de {item.Valor:C} foi devolvido ao orçamento.",
                    Usuario = nomeUsuario,
                    Ip = ipUsuario,
                    Data = DateTime.Now,
                    EmpresaId = empId,
                    EventoId = eventoId
                };
                _context.Logs.Add(log);

                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Item removido com sucesso!";
            }

            return RedirectToAction("Detalhes", "Eventos", new { id = eventoId });
        }
    }
}