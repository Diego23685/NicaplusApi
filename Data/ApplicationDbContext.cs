using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NicaplusApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NicaplusApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        // Único constructor requerido para la ejecución de la aplicación
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
        public DbSet<VariacionProducto> VariacionesProductos { get; set; }
        public DbSet<CodigoDigital> CodigosDigitales { get; set; }
        public DbSet<TasaDeCambio> TasasDeCambio { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            var userIdString = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var userName = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ClaimTypes.Name)?.Value ?? "Sistema";

            var tipoUsuario = _httpContextAccessor.HttpContext?.User?
                .FindFirst("TipoUsuario")?.Value;
            
            var ahoraNicaragua = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);

            var entradasModificadas = ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.Entity.GetType() != typeof(LogAuditoria))
                .ToList(); 
            
            foreach (var entry in entradasModificadas) 
            {
                string nombreRegistroAfectado = "N/A";
                var datosNuevos = new Dictionary<string, object?>();
                var datosViejos = new Dictionary<string, object?>();

                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    foreach (var prop in entry.CurrentValues.Properties)
                    {
                        datosNuevos[prop.Name] = entry.CurrentValues[prop.Name];
                    }
                }
                if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    foreach (var prop in entry.OriginalValues.Properties)
                    {
                        datosViejos[prop.Name] = entry.OriginalValues[prop.Name];
                    }
                }

                var valoresReferencia = entry.State == EntityState.Deleted ? entry.OriginalValues : entry.CurrentValues;

                var propiedadTexto = valoresReferencia.Properties
                    .FirstOrDefault(p => p.Name.ToLower() == "nombre" 
                                    || p.Name.ToLower() == "razonsocial" 
                                    || p.Name.ToLower() == "concepto"
                                    || p.Name.ToLower() == "descripcion"
                                    || p.Name.ToLower() == "email");

                if (propiedadTexto != null && valoresReferencia[propiedadTexto] != null)
                {
                    nombreRegistroAfectado = valoresReferencia[propiedadTexto]?.ToString() ?? "Sin datos";
                }
                else
                {
                    var propiedadId = valoresReferencia.Properties.FirstOrDefault(p => p.IsPrimaryKey() || p.Name.ToLower() == "id");
                    var idValor = propiedadId != null ? valoresReferencia[propiedadId]?.ToString() : "N/A";

                    var entidadNombre = entry.Entity.GetType().Name;

                    nombreRegistroAfectado = entidadNombre switch
                    {
                        "Venta" => $"Venta #{idValor}",
                        "DetalleVenta" => $"Detalle de Venta #{idValor}",
                        "Suscripcion" => $"Suscripción #{idValor}",
                        "PerfilCuenta" => $"Perfil #{idValor}",
                        "MovimientoCaja" => $"Movimiento por C${(valoresReferencia.Properties.Any(p => p.Name == "Monto") ? valoresReferencia["Monto"] : idValor)}",
                        _ => $"{entidadNombre} #{idValor}"
                    };
                }

                var metadataDetalle = new
                {
                    UsuarioNombre =
                        tipoUsuario == "Cliente"
                            ? $"Cliente: {userName}"
                            : tipoUsuario == "Administrador"
                                ? $"Usuario: {userName}"
                                : "Sistema",
                    TargetNombre = nombreRegistroAfectado,
                    ValoresNuevos = datosNuevos,
                    ValoresAnteriores = datosViejos
                };

                var nuevoLog = new LogAuditoria
                {
                    IdUsuario = tipoUsuario == "Administrador" && int.TryParse(userIdString, out var idUser) ? idUser : null,
                    IdCliente = tipoUsuario == "Cliente" && int.TryParse(userIdString, out var idClient) ? idClient : null,
                    TipoActor = tipoUsuario ?? "Sistema",
                    Accion = entry.State.ToString(),
                    TablaAfectada = entry.Entity.GetType().Name,
                    Detalles = JsonSerializer.Serialize(metadataDetalle),
                    FechaRegistro = DateTime.SpecifyKind(ahoraNicaragua, DateTimeKind.Unspecified)
                };

                Entry(nuevoLog).State = EntityState.Added;
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

            modelBuilder.Entity<VariacionProducto>()
                .HasOne(v => v.ProductoPadre)
                .WithMany(p => p.Variaciones)
                .HasForeignKey(v => v.ProductoPadreId)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<Venta>()
                .HasOne(v => v.Suscripcion)
                .WithMany(s => s.Ventas)
                .HasForeignKey(v => v.IdSuscripcion)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LogAuditoria>()
                .Property(l => l.FechaRegistro)
                .ValueGeneratedNever();
        }
    }

    // Fábrica para tiempo de diseño: Aísla a "dotnet ef" de la inyección de dependencias de ASP.NET
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Server=localhost;Database=nicaplus;User=root;Password=;";

            builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            return new ApplicationDbContext(builder.Options, new HttpContextAccessor());
        }
    }
}