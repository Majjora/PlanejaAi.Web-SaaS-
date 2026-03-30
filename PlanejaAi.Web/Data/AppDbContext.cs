using Microsoft.EntityFrameworkCore;
using PlanejaAi.Models;

namespace PlanejaAi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        
        public DbSet<Login> Logins { get; set; }

        
        public DbSet<Evento> Eventos { get; set; }

        public DbSet<Empresa> Empresas { get; set; }

        public DbSet<Log> Logs { get; set; }

        public DbSet<Funcionario> Funcionarios { get; set; }

    }
}