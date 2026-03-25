using Microsoft.EntityFrameworkCore;
using PlanejaAi.Models;

namespace PlanejaAi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Aqui avisamos ao contexto que a tabela login existe
        public DbSet<Login> Logins { get; set; }

        // A tabela Eventos que você já tinha criado
        public DbSet<Evento> Eventos { get; set; }

        public DbSet<Empresa> Empresas { get; set; }

        public DbSet<Log> Logs { get; set; }

    }
}