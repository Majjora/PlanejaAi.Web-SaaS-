using Microsoft.EntityFrameworkCore;
using PlanejaAi.Models;
using PlanejaAi.Helpers; 

namespace PlanejaAi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Fornecedor> Fornecedores { get; set; }
        public DbSet<CategoriaServico> CategoriasServico { get; set; }
        public DbSet<ProdutoFornecedor> ProdutosFornecedor { get; set; }
        public DbSet<Login> Logins { get; set; }
        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Funcionario> Funcionarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProdutoFornecedor>()
                .Property(p => p.ValorPadrao)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Empresa>()
                .Property(e => e.CpfCnpj)
                .HasConversion(
                    v => CriptografiaHelper.Criptografar(v),  
                    v => CriptografiaHelper.Descriptografar(v) 
                );

            modelBuilder.Entity<Funcionario>()
            .Property(f => f.Cpf)
            .HasConversion(
                v => CriptografiaHelper.Criptografar(v),
                v => CriptografiaHelper.Descriptografar(v)
            );

            modelBuilder.Entity<Fornecedor>()
            .Property(f => f.CnpjCpf)
            .HasConversion(
                v => CriptografiaHelper.Criptografar(v),
                v => CriptografiaHelper.Descriptografar(v)
            );
        }
    }
}