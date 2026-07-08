using Microsoft.EntityFrameworkCore;
using NicaplusApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NicaplusApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor) 
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
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
        public DbSet<Suscripcion> Suscripciones { get; set; }
        public DbSet<CuentaPorCobrar> CuentasPorCobrar { get; set; }
        public DbSet<CuentaPorPagar> CuentasPorPagar { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; } 
        public DbSet<PerfilCuenta> PerfilesCuentas { get; set; }
        public DbSet<CompraProveedor> ComprasProveedores { get; set; }
        public DbSet<DetalleCompraProveedor> DetallesComprasProveedores { get; set; }
        public DbSet<TicketSoporte> TicketsSoporte { get; set; }
        public DbSet<GarantiaTicket> GarantiasTickets { get; set; }
        public DbSet<MovimientoCaja> MovimientosCaja { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }
        public DbSet<ConfiguracionMensaje> ConfiguracionesMensajes { get; set; }
        public DbSet<Renovacion> Renovaciones { get; set; }
        public DbSet<Cancelacion> Cancelaciones { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            // 1. Obtener el ID del usuario desde el Token JWT o Sesión
            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // Intentar obtener también el Nombre del usuario si lo guardaste en los Claims del Token
            var userName = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario del Sistema";
            
            var entradasModificadas = ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.Entity.GetType() != typeof(LogAuditoria))
                .ToList(); 
            
            foreach (var entry in entradasModificadas) 
            {
                string nombreRegistroAfectado = "N/A";
                var datosNuevos = new Dictionary<string, object>();
                var datosViejos = new Dictionary<string, object>();

                // Buscar si la entidad tiene alguna propiedad que actúe como "Nombre" o "Descripción" para identificarla
                var propiedadNombre = entry.CurrentValues.Properties
                    .FirstOrDefault(p => p.Name.ToLower() == "nombre" || p.Name.ToLower() == "razonsocial");

                if (propiedadNombre != null && entry.State != EntityState.Deleted)
                {
                    nombreRegistroAfectado = entry.CurrentValues[propiedadNombre]?.ToString() ?? "Sin nombre";
                }

                // Capturar los valores (útil para auditorías avanzadas)
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    foreach (var prop in entry.CurrentValues.Properties)
                    {
                        // CORRECCIÓN: Controlamos el valor nulo usando DBNull.Value para el diccionario
                        datosNuevos[prop.Name] = entry.CurrentValues[prop.Name] ?? DBNull.Value;
                    }
                }
                if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    foreach (var prop in entry.OriginalValues.Properties)
                    {
                        // CORRECCIÓN: Controlamos el valor nulo usando DBNull.Value para el diccionario
                        datosViejos[prop.Name] = entry.OriginalValues[prop.Name] ?? DBNull.Value;
                    }
                }

                // Estructuramos un JSON limpio para la columna Detalles
                var metadataDetalle = new
                {
                    UsuarioNombre = userName,
                    TargetNombre = nombreRegistroAfectado,
                    ValoresNuevos = datosNuevos,
                    ValoresAnteriores = datosViejos
                };

                LogsAuditoria.Add(new LogAuditoria {
                    IdUsuario = userIdString != null ? int.Parse(userIdString) : 0,
                    Accion = entry.State.ToString(),
                    TablaAfectada = entry.Entity.GetType().Name,
                    // Serializamos la estructura a string JSON
                    Detalles = JsonSerializer.Serialize(metadataDetalle),
                    FechaRegistro = DateTime.UtcNow // Sincronizado a UTC con tus otros modelos
                });
            }
            
            return await base.SaveChangesAsync(ct);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Rol>().HasData(
                new Rol { Id = 1, NombreRol = "Administrador" },
                new Rol { Id = 2, NombreRol = "Socio" },
                new Rol { Id = 3, NombreRol = "Ventas" },
                new Rol { Id = 4, NombreRol = "Soporte" }
            );
            modelBuilder.Entity<Producto>(entity => {
                entity.Property(p => p.PrecioVenta).HasColumnType("decimal(18,2)");
                entity.Property(p => p.PrecioCosto).HasColumnType("decimal(18,2)");
                entity.Property(p => p.Nombre).HasMaxLength(255).IsRequired();
                entity.Property(p => p.Descripcion).HasMaxLength(500);
                entity.Property(p => p.ImagenUrl).HasColumnType("longtext"); 
            });

            modelBuilder.Entity<Suscripcion>()
                .HasOne(s => s.PerfilCuenta)
                .WithMany(p => p.Suscripciones)
                .HasForeignKey(s => s.IdPerfilCuenta)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Renovacion>()
                .HasOne(r => r.Suscripcion)
                .WithMany()
                .HasForeignKey(r => r.IdSuscripcion)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Renovacion>()
                .HasOne(r => r.Cliente)
                .WithMany()
                .HasForeignKey(r => r.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Renovacion>()
                .HasOne(r => r.Suscripcion)
                .WithMany()
                .HasForeignKey(r => r.IdSuscripcion)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Renovacion>()
                .HasOne(r => r.Cliente)
                .WithMany()
                .HasForeignKey(r => r.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}