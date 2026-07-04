using Microsoft.EntityFrameworkCore;
using NicaplusApi.Models;

namespace NicaplusApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetallesVentas { get; set; }
        public DbSet<OrdenServicio> OrdenesServicio { get; set; }
        public DbSet<Juego> Juegos { get; set; }
        public DbSet<Categoria> Categorias { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Rol>().HasData(
                new Rol { Id = 1, NombreRol = "Admin" },
                new Rol { Id = 2, NombreRol = "Cajero" },
                new Rol { Id = 3, NombreRol = "Técnico" }
            );

            modelBuilder.Entity<Producto>(entity =>
            {
                // Optimización de tipos numéricos
                entity.Property(p => p.PrecioVenta).HasColumnType("decimal(18,2)");
                entity.Property(p => p.PrecioCosto).HasColumnType("decimal(18,2)");

                // OPTIMIZACIÓN CRÍTICA: Forzar longitudes fijas en lugar de longtext
                entity.Property(p => p.Nombre).HasMaxLength(255).IsRequired();
                entity.Property(p => p.Descripcion).HasMaxLength(500);
                
                // longtext solo para la imagen por si almacena Base64 pesadas, pero ya tiene el timeout asignado
                entity.Property(p => p.ImagenUrl).HasColumnType("longtext"); 
            });
        }
    }
}