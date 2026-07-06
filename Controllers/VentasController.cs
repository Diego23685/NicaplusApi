using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using Microsoft.AspNetCore.Authorization;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public VentasController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        [Authorize(Roles = "Administrador,Socio,Ventas")]
        public async Task<ActionResult<IEnumerable<Venta>>> Get() => 
            await _context.Ventas.Include(v => v.Detalles).Include(v => v.Cliente).OrderByDescending(v => v.Id).ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Venta>> Post([FromBody] Venta venta)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (venta.FechaVenta == default(DateTime) || venta.FechaVenta.Year == 1)
                {
                    venta.FechaVenta = DateTime.UtcNow;
                }

                _context.Ventas.Add(venta);

                // 1. PROCESAMIENTO LINEAL DE LOS ARTÍCULOS EN EL CARRITO
                foreach (var detalle in venta.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null)
                    {
                        // A. Control de inventario físico ordinario
                        if (!prod.EsDigital && !prod.RequiereServicio)
                        {
                            if (prod.StockActual < detalle.Cantidad)
                                return BadRequest($"Stock insuficiente para el producto: {prod.Nombre}");
                            
                            prod.StockActual -= detalle.Cantidad;
                        }

                        // B. Control y generación de Suscripciones/Renovaciones recurrentes y asignación de perfiles
                        if (prod.EsSuscripcion)
                        {
                            if (!venta.IdCliente.HasValue || venta.IdCliente.Value == 0)
                            {
                                return BadRequest($"El producto '{prod.Nombre}' es una suscripción/renovación. Debe asociar obligatoriamente un cliente a la venta.");
                            }

                            string tipoSuscripcion = prod.EsDigital ? "Digital" : 
                                                    prod.RequiereServicio ? "Mantenimiento" : "Físico";

                            // Extracción de los días personalizados inyectados desde la Caja POS
                            int diasEfectivos = prod.DiasDuracion > 0 ? prod.DiasDuracion : 30;
                            if (!string.IsNullOrEmpty(detalle.MetadataDigital) && detalle.MetadataDigital.Contains("DIAS:"))
                            {
                                var partes = detalle.MetadataDigital.Split('|');
                                foreach (var parte in partes)
                                {
                                    if (parte.StartsWith("DIAS:"))
                                    {
                                        int.TryParse(parte.Replace("DIAS:", ""), out diasEfectivos);
                                    }
                                }
                            }

                            // LÓGICA DE ASIGNACIÓN AUTOMÁTICA DE PERFILES
                            // Buscamos si el producto base maneja perfiles independientes y tomamos el primero libre
                            var perfilDisponible = await _context.PerfilesCuentas
                                .FirstOrDefaultAsync(p => p.IdProducto == prod.Id && !p.Ocupado);

                            string credencialesFinales = "";

                            if (perfilDisponible != null)
                            {
                                // Asignamos y bloqueamos el perfil para este cliente
                                perfilDisponible.Ocupado = true;
                                perfilDisponible.IdClienteAsignado = venta.IdCliente.Value;
                                _context.PerfilesCuentas.Update(perfilDisponible);

                                // Construimos la metadata con los datos de acceso del perfil para el ticket térmico
                                credencialesFinales = $"PERFIL: {perfilDisponible.NombrePerfil} | PIN: {perfilDisponible.PIN} | Acceso: {perfilDisponible.CorreoCuenta} / {perfilDisponible.PasswordCuenta}";
                                
                                // Inyectamos los datos del perfil directamente en el detalle de la venta para que persistan en el ticket
                                detalle.MetadataDigital = credencialesFinales;
                            }
                            else 
                            {
                                // Si el producto tiene perfiles registrados pero todos están en true (Ocupado), frenamos la venta
                                bool manejaPerfiles = await _context.PerfilesCuentas.AnyAsync(p => p.IdProducto == prod.Id);
                                if (manejaPerfiles)
                                {
                                    return BadRequest($"Operación Cancelada: Ya no quedan pantallas/perfiles disponibles para la cuenta '{prod.Nombre}'.");
                                }
                                
                                // Fallback clásico si es un servicio digital recurrente que no se fracciona en pantallas
                                credencialesFinales = string.IsNullOrEmpty(detalle.MetadataDigital) 
                                    ? $"Registrado desde Caja POS ({diasEfectivos} días)" 
                                    : $"{detalle.MetadataDigital}";
                            }

                            var nuevaSuscripcion = new Suscripcion
                            {
                                IdCliente = venta.IdCliente.Value,
                                NombreServicio = prod.Nombre,
                                TipoSuscripcion = tipoSuscripcion,
                                IdProducto = prod.Id,
                                CostoRenovacion = prod.PrecioVenta,
                                FechaInicio = venta.FechaVenta,
                                FechaVencimiento = venta.FechaVenta.AddDays(diasEfectivos), 
                                Estado = "Activa",
                                DetallesCredenciales = credencialesFinales
                            };

                            // Registrar de manera automatizada el Ingreso en la Caja Contable
                            var ingresoCaja = new MovimientoCaja
                            {
                                Fecha = venta.FechaVenta,
                                Tipo = "Ingreso",
                                Concepto = "Venta",
                                Monto = venta.Total,
                                Detalle = $"Facturación de Orden #{venta.Id}. Método: {venta.MetodoPago}",
                                IdVenta = venta.Id
                            };
                            _context.MovimientosCaja.Add(ingresoCaja);

                            _context.Suscripciones.Add(nuevaSuscripcion);
                        }
                    }
                }

                // 2. CONTROL CONTABLE GENERAL: Desvío a Cuentas por Cobrar si es venta al Crédito
                if (venta.MetodoPago == "Crédito")
                {
                    if (!venta.IdCliente.HasValue || venta.IdCliente.Value == 0)
                    {
                        return BadRequest("No se puede procesar una venta al crédito bajo un cliente genérico de mostrador. Seleccione un cliente real.");
                    }

                    // Si el usuario envió una fecha manual en la petición, se usa. Si no, cae en el fallback de 15 días.
                    DateTime fechaVencimientoFinal = venta.FechaVencimientoCreditoManual.HasValue 
                        ? venta.FechaVencimientoCreditoManual.Value 
                        : venta.FechaVenta.AddDays(15);

                    var nuevaCuentaCobrar = new CuentaPorCobrar
                    {
                        IdCliente = venta.IdCliente.Value,
                        IdVenta = venta.Id, 
                        MontoTotal = venta.Total,
                        SaldoPendiente = venta.Total, 
                        FechaEmision = venta.FechaVenta,
                        FechaVencimiento = fechaVencimientoFinal, // <--- ASIGNACIÓN DINÁMICA
                        Estado = "Pendiente"
                    };

                    _context.CuentasPorCobrar.Add(nuevaCuentaCobrar);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(venta);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al procesar la transacción financiera: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Put(int id, [FromBody] Venta ventaActualizada)
        {
            if (id != ventaActualizada.Id) return BadRequest("IDs no coinciden.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ventaOriginal = await _context.Ventas.Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);
                
                if (ventaOriginal == null) return NotFound("Venta no encontrada.");

                // 1. REVERSIÓN DE STOCK
                foreach (var detalleOrig in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        prod.StockActual += detalleOrig.Cantidad;
                    }
                }

                // 2. APLICAR NUEVOS VALORES FINANCIEROS
                ventaOriginal.MetodoPago = ventaActualizada.MetodoPago;
                ventaOriginal.IdCliente = ventaActualizada.IdCliente;

                // CORREGIDO: Remoción limpia de detalles desde la colección sin requerir DbSet explícito
                _context.RemoveRange(ventaOriginal.Detalles);

                // 3. DESCONTAR EL NUEVO STOCK E INYECTAR NUEVOS DETALLES
                ventaOriginal.Detalles = ventaActualizada.Detalles;
                foreach (var nuevoDetalle in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(nuevoDetalle.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        if (prod.StockActual < nuevoDetalle.Cantidad)
                            return BadRequest($"Stock insuficiente tras reajuste para: {prod.Nombre}");
                        
                        prod.StockActual -= nuevoDetalle.Cantidad;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Fallo al revertir o recalcular stock y flujos de caja.");
            }
        }
    }
}