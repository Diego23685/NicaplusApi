using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VentasController> _logger;

        public VentasController(
            ApplicationDbContext context,
            ILogger<VentasController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private DateTime GetNicaraguaTime()
        {
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int id) ? id : 1;
        }

        private int ExtraerDiasSuscripcion(string metadataDigital, int diasDuracionBase)
        {
            if (!string.IsNullOrEmpty(metadataDigital) && metadataDigital.StartsWith("DIAS:"))
            {
                var partes = metadataDigital.Split('|');
                var diasStr = partes[0].Replace("DIAS:", "").Trim();
                if (int.TryParse(diasStr, out int diasParseados) && diasParseados > 0)
                {
                    return diasParseados;
                }
            }
            return diasDuracionBase > 0 ? diasDuracionBase : 30;
        }

        // GET: api/Ventas
        [HttpGet]
        [Authorize(Roles = "Administrador,Socio,Ventas")]
        public async Task<IActionResult> Get()
        {
            try
            {
                var ventas = await _context.Ventas
                    .AsNoTracking()
                    .Include(v => v.Cliente)
                    .Include(v => v.Usuario)
                    .Include(v => v.Detalles!)
                        .ThenInclude(d => d.Producto)
                    .OrderByDescending(v => v.Id)
                    .Select(v => new VentaResumenDto
                    {
                        Id = v.Id,
                        FechaVenta = v.FechaVenta.ToString("yyyy-MM-dd HH:mm:ss"),
                        IdCliente = v.IdCliente,
                        ClienteNombre = v.Cliente != null ? v.Cliente.Nombre : "Cliente General / Público",
                        Operador = v.Usuario != null ? v.Usuario.Nombre : "Sistema",
                        MetodoPago = v.MetodoPago,
                        Total = v.Total,
                        Detalles = v.Detalles.Select(d => new DetalleVentaResumenDto
                        {
                            Id = d.Id,
                            IdProducto = d.IdProducto,
                            ProductoNombre = d.Producto != null ? d.Producto.Nombre : "Producto General",
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            Descuento = d.Descuento,
                            SubTotal = d.SubTotal,
                            MetadataDigital = d.MetadataDigital ?? string.Empty
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(ventas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de ventas.");
                return StatusCode(500, new { mensaje = "Error interno al consultar las ventas." });
            }
        }

        // GET: api/Ventas/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrador,Socio,Ventas")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var venta = await _context.Ventas
                    .AsNoTracking()
                    .Include(v => v.Cliente)
                    .Include(v => v.Usuario)
                    .Include(v => v.Detalles!)
                        .ThenInclude(d => d.Producto)
                    .Where(v => v.Id == id)
                    .Select(v => new VentaResumenDto
                    {
                        Id = v.Id,
                        FechaVenta = v.FechaVenta.ToString("yyyy-MM-dd HH:mm:ss"),
                        IdCliente = v.IdCliente,
                        ClienteNombre = v.Cliente != null ? v.Cliente.Nombre : "Cliente General / Público",
                        Operador = v.Usuario != null ? v.Usuario.Nombre : "Sistema",
                        MetodoPago = v.MetodoPago,
                        Total = v.Total,
                        Detalles = v.Detalles.Select(d => new DetalleVentaResumenDto
                        {
                            Id = d.Id,
                            IdProducto = d.IdProducto,
                            ProductoNombre = d.Producto != null ? d.Producto.Nombre : "Producto General",
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            Descuento = d.Descuento,
                            SubTotal = d.SubTotal,
                            MetadataDigital = d.MetadataDigital ?? string.Empty
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (venta == null)
                    return NotFound(new { mensaje = "Venta no encontrada." });

                return Ok(venta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la venta #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar la venta." });
            }
        }

        // POST: api/Ventas
        [HttpPost]
        [Authorize(Roles = "Administrador,Socio,Ventas")]
        public async Task<IActionResult> Post([FromBody] CrearVentaDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                DateTime fechaEfectiva = (dto.FechaVenta.HasValue && dto.FechaVenta.Value != default) 
                    ? dto.FechaVenta.Value 
                    : ahoraNicaragua;

                int idOperador = GetCurrentUserId();
                int? idClienteFinal = (dto.IdCliente.HasValue && dto.IdCliente.Value > 0) ? dto.IdCliente : null;

                if (dto.MetodoPago == "Crédito" && !idClienteFinal.HasValue)
                {
                    return BadRequest(new { mensaje = "Las ventas al crédito requieren obligatoriamente un cliente registrado." });
                }

                var nuevaVenta = new Venta
                {
                    FechaVenta = fechaEfectiva,
                    IdCliente = idClienteFinal,
                    IdUsuario = idOperador,
                    MetodoPago = dto.MetodoPago.Trim(),
                    Total = 0m
                };

                _context.Ventas.Add(nuevaVenta);
                await _context.SaveChangesAsync();

                decimal totalAcumulado = 0m;

                foreach (var itemDto in dto.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(itemDto.IdProducto);
                    if (prod == null)
                        return BadRequest(new { mensaje = $"El producto con ID {itemDto.IdProducto} no existe." });

                    VariacionProducto? variacionElegida = null;

                    if (itemDto.IdVariacion.HasValue && itemDto.IdVariacion.Value > 0)
                    {
                        variacionElegida = await _context.VariacionesProductos.FindAsync(itemDto.IdVariacion.Value);
                        if (variacionElegida == null)
                            return BadRequest(new { mensaje = $"La variación elegida no existe en el catálogo." });

                        if (variacionElegida.StockActual < itemDto.Cantidad)
                        {
                            return BadRequest(new { mensaje = $"Stock insuficiente para: {prod.Nombre} ({variacionElegida.NombreVariacion}). Disponible: {variacionElegida.StockActual}" });
                        }

                        variacionElegida.StockActual -= itemDto.Cantidad;
                        if (variacionElegida.StockActual <= 0) variacionElegida.Estado = "Agotado";
                        _context.VariacionesProductos.Update(variacionElegida);
                    }
                    else if (prod.ControlaStock && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        if (prod.StockActual < itemDto.Cantidad)
                            return BadRequest(new { mensaje = $"Stock insuficiente para: {prod.Nombre}. Disponible: {prod.StockActual}" });

                        prod.StockActual -= itemDto.Cantidad;
                        if (prod.StockActual <= 0) prod.Estado = "Agotado";
                        _context.Productos.Update(prod);
                    }

                    var detalleVenta = new DetalleVenta
                    {
                        IdVenta = nuevaVenta.Id,
                        IdProducto = prod.Id,
                        VariacionId = itemDto.IdVariacion,
                        Cantidad = itemDto.Cantidad,
                        PrecioUnitario = itemDto.PrecioUnitario,
                        Descuento = itemDto.Descuento,
                        SubTotal = (itemDto.Cantidad * itemDto.PrecioUnitario) - itemDto.Descuento
                    };

                    if (prod.EsSuscripcion)
                    {
                        if (!idClienteFinal.HasValue)
                            return BadRequest(new { mensaje = $"El servicio '{prod.Nombre}' requiere obligatoriamente asignar un cliente." });

                        var grupoValido = await _context.PerfilesCuentas
                            .Where(p => p.IdProducto == prod.Id && !p.Ocupado && !string.IsNullOrEmpty(p.AccountGroupKey))
                            .GroupBy(p => p.AccountGroupKey)
                            .Where(g => g.Count() >= itemDto.Cantidad)
                            .Select(g => g.Key)
                            .FirstOrDefaultAsync();

                        List<PerfilCuenta> perfilesAsignados = new List<PerfilCuenta>();

                        if (!string.IsNullOrEmpty(grupoValido))
                        {
                            perfilesAsignados = await _context.PerfilesCuentas
                                .Where(p => p.IdProducto == prod.Id && !p.Ocupado && p.AccountGroupKey == grupoValido)
                                .Take(itemDto.Cantidad)
                                .ToListAsync();
                        }
                        else
                        {
                            perfilesAsignados = await _context.PerfilesCuentas
                                .Where(p => p.IdProducto == prod.Id && !p.Ocupado)
                                .Take(itemDto.Cantidad)
                                .ToListAsync();
                        }

                        if (perfilesAsignados.Count < itemDto.Cantidad)
                        {
                            return BadRequest(new { mensaje = $"No existen suficientes pantallas/perfiles disponibles para '{prod.Nombre}'." });
                        }

                        var metadataList = new List<string>();

                        foreach (var perfil in perfilesAsignados)
                        {
                            perfil.Ocupado = true;
                            perfil.IdClienteAsignado = idClienteFinal.Value;
                            perfil.EstadoPerfil = "Asignado";
                            perfil.FechaAsignacion = fechaEfectiva;
                            _context.PerfilesCuentas.Update(perfil);

                            var credencial = $"PERFIL: {perfil.NombrePerfil} | PIN: {perfil.PIN} | Acceso: {perfil.CorreoCuenta} / {perfil.PasswordCuenta}";
                            metadataList.Add(credencial);

                            int diasEfectivos = ExtraerDiasSuscripcion(itemDto.MetadataDigital, prod.DiasDuracion);
                            var fechaVenc = fechaEfectiva.AddDays(diasEfectivos);

                            var suscripcion = new Suscripcion
                            {
                                IdCliente = idClienteFinal.Value,
                                IdVenta = nuevaVenta.Id,
                                NombreServicio = $"{prod.Nombre} ({perfil.NombrePerfil})",
                                TipoSuscripcion = "Digital",
                                IdProducto = prod.Id,
                                IdPerfilCuenta = perfil.Id,
                                CostoRenovacion = itemDto.PrecioUnitario,
                                FechaInicio = fechaEfectiva,
                                FechaVencimiento = fechaVenc,
                                Estado = fechaVenc <= ahoraNicaragua ? "Vencida" : "Activa", 
                                DetallesCredenciales = credencial
                            };
                            _context.Suscripciones.Add(suscripcion);
                        }

                        detalleVenta.MetadataDigital = string.Join(" || ", metadataList);
                    }

                    _context.DetallesVentas.Add(detalleVenta);
                    totalAcumulado += detalleVenta.SubTotal;
                }

                nuevaVenta.Total = totalAcumulado;
                _context.Ventas.Update(nuevaVenta);

                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = fechaEfectiva,
                    Tipo = "Ingreso",
                    Concepto = "Venta",
                    Monto = totalAcumulado,
                    Detalle = $"Facturación de Orden #{nuevaVenta.Id}. Método: {nuevaVenta.MetodoPago}",
                    IdVenta = nuevaVenta.Id
                };
                _context.MovimientosCaja.Add(movimientoCaja);

                if (nuevaVenta.MetodoPago == "Crédito" && idClienteFinal.HasValue)
                {
                    var cpc = new CuentaPorCobrar
                    {
                        IdCliente = idClienteFinal.Value,
                        IdVenta = nuevaVenta.Id,
                        MontoTotal = totalAcumulado,
                        SaldoPendiente = totalAcumulado,
                        FechaEmision = fechaEfectiva,
                        FechaVencimiento = dto.FechaVencimientoCreditoManual ?? fechaEfectiva.AddDays(15),
                        Estado = "Pendiente"
                    };
                    _context.CuentasPorCobrar.Add(cpc);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetById), new { id = nuevaVenta.Id }, new
                {
                    mensaje = "Venta registrada con éxito.",
                    idVenta = nuevaVenta.Id,
                    total = totalAcumulado
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al procesar el registro de venta.");
                return StatusCode(500, new { mensaje = "Error interno al procesar la transacción de venta." });
            }
        }

        // PUT: api/Ventas/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Put(int id, [FromBody] ActualizarVentaDto dto)
        {
            if (id != dto.Id)
                return BadRequest(new { mensaje = "El ID de la URL no coincide con el cuerpo de la solicitud." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ventaOriginal = await _context.Ventas
                    .Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (ventaOriginal == null)
                    return NotFound(new { mensaje = "La venta especificada no existe." });

                var ahoraNicaragua = GetNicaraguaTime();
                int? idClienteFinal = (dto.IdCliente.HasValue && dto.IdCliente.Value > 0) ? dto.IdCliente : null;

                // ==========================================
                // 1. REVERSIÓN DE LA VENTA ANTERIOR
                // ==========================================
                foreach (var detalleOrig in ventaOriginal.Detalles)
                {
                    if (detalleOrig.VariacionId.HasValue && detalleOrig.VariacionId.Value > 0)
                    {
                        var variacion = await _context.VariacionesProductos.FindAsync(detalleOrig.VariacionId.Value);
                        if (variacion != null)
                        {
                            variacion.StockActual += detalleOrig.Cantidad;
                            if (variacion.Estado == "Agotado" && variacion.StockActual > 0) variacion.Estado = "Activo";
                            _context.VariacionesProductos.Update(variacion);
                        }
                    }
                    else
                    {
                        var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                        if (prod != null && prod.ControlaStock && !prod.TieneVariaciones)
                        {
                            prod.StockActual += detalleOrig.Cantidad;
                            if (prod.Estado == "Agotado" && prod.StockActual > 0) prod.Estado = "Activo";
                            _context.Productos.Update(prod);
                        }
                    }
                }

                // Obtener suscripciones anteriores asociadas a esta venta
                var suscripcionesViejas = await _context.Suscripciones
                    .Where(s => s.IdVenta == id)
                    .ToListAsync();

                var idsPerfilesPropios = suscripcionesViejas
                    .Where(s => s.IdPerfilCuenta.HasValue)
                    .Select(s => s.IdPerfilCuenta!.Value)
                    .ToList();

                // Rescatar perfiles asignados si no se encontraron vía IdPerfilCuenta
                if (!idsPerfilesPropios.Any() && ventaOriginal.IdCliente.HasValue)
                {
                    var idsProductosVenta = ventaOriginal.Detalles.Select(d => d.IdProducto).ToList();
                    var idsRescatados = await _context.PerfilesCuentas
                        .Where(p => p.IdClienteAsignado == ventaOriginal.IdCliente.Value && idsProductosVenta.Contains(p.IdProducto))
                        .Select(p => p.Id)
                        .ToListAsync();

                    idsPerfilesPropios.AddRange(idsRescatados);
                }

                // Liberar temporalmente los perfiles para que la re-asignación posterior los pueda tomar
                var perfilesAfectados = await _context.PerfilesCuentas
                    .Where(p => idsPerfilesPropios.Contains(p.Id))
                    .ToListAsync();

                foreach (var p in perfilesAfectados)
                {
                    p.Ocupado = false;
                    p.IdClienteAsignado = null;
                    p.EstadoPerfil = "Disponible";
                    _context.PerfilesCuentas.Update(p);
                }

                // Limpiar los detalles de venta anteriores
                _context.DetallesVentas.RemoveRange(ventaOriginal.Detalles);
                await _context.SaveChangesAsync();

                // ==========================================
                // 2. APLICACIÓN DE NUEVOS VALORES Y RE-ASIGNACIÓN
                // ==========================================
                ventaOriginal.IdCliente = idClienteFinal;
                ventaOriginal.MetodoPago = dto.MetodoPago.Trim();

                decimal nuevoTotalCalculado = 0m;

                foreach (var itemDto in dto.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(itemDto.IdProducto);
                    if (prod == null)
                        return BadRequest(new { mensaje = $"El producto con ID {itemDto.IdProducto} no existe." });

                    // Descuento de inventario según tipo de producto/variante
                    if (itemDto.IdVariacion.HasValue && itemDto.IdVariacion.Value > 0)
                    {
                        var variacionElegida = await _context.VariacionesProductos.FindAsync(itemDto.IdVariacion.Value);
                        if (variacionElegida == null)
                            return BadRequest(new { mensaje = "La variación elegida no existe en el catálogo." });

                        if (variacionElegida.StockActual < itemDto.Cantidad)
                            return BadRequest(new { mensaje = $"Stock insuficiente para: {prod.Nombre} ({variacionElegida.NombreVariacion}). Disponible: {variacionElegida.StockActual}" });

                        variacionElegida.StockActual -= itemDto.Cantidad;
                        if (variacionElegida.StockActual <= 0) variacionElegida.Estado = "Agotado";
                        _context.VariacionesProductos.Update(variacionElegida);
                    }
                    else if (prod.ControlaStock && !prod.EsDigital && !prod.RequiereServicio && !prod.TieneVariaciones)
                    {
                        if (prod.StockActual < itemDto.Cantidad)
                            return BadRequest(new { mensaje = $"Stock insuficiente para: {prod.Nombre}. Disponible: {prod.StockActual}" });

                        prod.StockActual -= itemDto.Cantidad;
                        if (prod.StockActual <= 0) prod.Estado = "Agotado";
                        _context.Productos.Update(prod);
                    }

                    var nuevoDetalle = new DetalleVenta
                    {
                        IdVenta = id,
                        IdProducto = prod.Id,
                        VariacionId = itemDto.IdVariacion,
                        Cantidad = itemDto.Cantidad,
                        PrecioUnitario = itemDto.PrecioUnitario,
                        Descuento = itemDto.Descuento,
                        SubTotal = (itemDto.Cantidad * itemDto.PrecioUnitario) - itemDto.Descuento
                    };

                    if (prod.EsSuscripcion)
                    {
                        if (!idClienteFinal.HasValue)
                            return BadRequest(new { mensaje = $"El servicio '{prod.Nombre}' requiere obligatoriamente un cliente." });

                        // 🟢 CORRECCIÓN: Buscar la suscripción anterior específica de este detalle de venta para conservar su perfil exacto
                        var suscripcionAnterior = suscripcionesViejas.FirstOrDefault(s => s.IdProducto == prod.Id && s.IdVenta == id);
                        
                        PerfilCuenta? perfilAsignado = null;

                        if (suscripcionAnterior != null && suscripcionAnterior.IdPerfilCuenta.HasValue)
                        {
                            // Reutilizar el mismo perfil que ya tenía asignado esta suscripción
                            perfilAsignado = await _context.PerfilesCuentas.FindAsync(suscripcionAnterior.IdPerfilCuenta.Value);
                        }

                        if (perfilAsignado == null)
                        {
                            // Si es nuevo o no se encuentra, tomar uno disponible del pool
                            perfilAsignado = await _context.PerfilesCuentas
                                .Where(p => p.IdProducto == prod.Id && !p.Ocupado)
                                .FirstOrDefaultAsync();
                        }

                        if (perfilAsignado == null)
                        {
                            return BadRequest(new { mensaje = $"No existen suficientes pantallas disponibles para '{prod.Nombre}'." });
                        }

                        perfilAsignado.Ocupado = true;
                        perfilAsignado.IdClienteAsignado = idClienteFinal.Value;
                        perfilAsignado.EstadoPerfil = "Asignado";
                        perfilAsignado.FechaAsignacion = ventaOriginal.FechaVenta;
                        _context.PerfilesCuentas.Update(perfilAsignado);

                        var credencial = $"PERFIL: {perfilAsignado.NombrePerfil} | PIN: {perfilAsignado.PIN} | Acceso: {perfilAsignado.CorreoCuenta} / {perfilAsignado.PasswordCuenta}";
                        nuevoDetalle.MetadataDigital = credencial;

                        int diasEfectivos = ExtraerDiasSuscripcion(itemDto.MetadataDigital, prod.DiasDuracion);

                        if (suscripcionAnterior != null)
                        {
                            // Actualizar estrictamente la suscripción ligada a este perfil/venta
                            suscripcionAnterior.IdCliente = idClienteFinal.Value;
                            suscripcionAnterior.NombreServicio = $"{prod.Nombre} ({perfilAsignado.NombrePerfil})";
                            suscripcionAnterior.IdPerfilCuenta = perfilAsignado.Id;
                            suscripcionAnterior.CostoRenovacion = itemDto.PrecioUnitario;
                            suscripcionAnterior.FechaVencimiento = ventaOriginal.FechaVenta.AddDays(diasEfectivos);
                            suscripcionAnterior.Estado = "Activa";
                            suscripcionAnterior.DetallesCredenciales = credencial;

                            _context.Suscripciones.Update(suscripcionAnterior);
                        }
                        else
                        {
                            var nuevaSuscripcion = new Suscripcion
                            {
                                IdCliente = idClienteFinal.Value,
                                IdVenta = id,
                                NombreServicio = $"{prod.Nombre} ({perfilAsignado.NombrePerfil})",
                                TipoSuscripcion = "Digital",
                                IdProducto = prod.Id,
                                IdPerfilCuenta = perfilAsignado.Id,
                                CostoRenovacion = itemDto.PrecioUnitario,
                                FechaInicio = ventaOriginal.FechaVenta,
                                FechaVencimiento = ventaOriginal.FechaVenta.AddDays(diasEfectivos),
                                Estado = "Activa",
                                DetallesCredenciales = credencial
                            };
                            _context.Suscripciones.Add(nuevaSuscripcion);
                        }
                    }

                    _context.DetallesVentas.Add(nuevoDetalle);
                    nuevoTotalCalculado += nuevoDetalle.SubTotal;
                }

                ventaOriginal.Total = nuevoTotalCalculado;
                _context.Ventas.Update(ventaOriginal);

                // ==========================================
                // 3. SINCRONIZACIÓN CONTABLE
                // ==========================================
                var movimientoCaja = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdVenta == id);
                if (movimientoCaja != null)
                {
                    movimientoCaja.Monto = nuevoTotalCalculado;
                    movimientoCaja.Detalle = $"Facturación (Editada) de Orden #{id}. Método: {ventaOriginal.MetodoPago}";
                    _context.MovimientosCaja.Update(movimientoCaja);
                }

                var cpc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.IdVenta == id);
                if (ventaOriginal.MetodoPago == "Crédito" && idClienteFinal.HasValue)
                {
                    if (cpc != null)
                    {
                        cpc.IdCliente = idClienteFinal.Value;
                        cpc.MontoTotal = nuevoTotalCalculado;
                        cpc.SaldoPendiente = nuevoTotalCalculado;
                        _context.CuentasPorCobrar.Update(cpc);
                    }
                    else
                    {
                        _context.CuentasPorCobrar.Add(new CuentaPorCobrar
                        {
                            IdCliente = idClienteFinal.Value,
                            IdVenta = id,
                            MontoTotal = nuevoTotalCalculado,
                            SaldoPendiente = nuevoTotalCalculado,
                            FechaEmision = ventaOriginal.FechaVenta,
                            FechaVencimiento = dto.FechaVencimientoCreditoManual ?? ventaOriginal.FechaVenta.AddDays(15),
                            Estado = "Pendiente"
                        });
                    }
                }
                else if (cpc != null)
                {
                    _context.CuentasPorCobrar.Remove(cpc);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Venta y registros contables asociados actualizados exitosamente." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error editando la venta #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al editar la orden de venta." });
            }
        }

        // DELETE: api/Ventas/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Delete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null)
                    return NotFound(new { mensaje = "La venta especificada no existe." });

                // =========================================================
                // 1. RESTAURACIÓN DE INVENTARIO (PRODUCTOS PLANOS Y VARIANTES)
                // =========================================================
                foreach (var detalle in venta.Detalles)
                {
                    if (detalle.VariacionId.HasValue && detalle.VariacionId.Value > 0)
                    {
                        var variacion = await _context.VariacionesProductos.FindAsync(detalle.VariacionId.Value);
                        if (variacion != null)
                        {
                            variacion.StockActual += detalle.Cantidad;
                            if (variacion.Estado == "Agotado" && variacion.StockActual > 0)
                            {
                                variacion.Estado = "Activo";
                            }
                            _context.VariacionesProductos.Update(variacion);
                        }
                    }
                    else
                    {
                        var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                        if (prod != null && prod.ControlaStock)
                        {
                            prod.StockActual += detalle.Cantidad;
                            if (prod.Estado == "Agotado" && prod.StockActual > 0)
                            {
                                prod.Estado = "Activo";
                            }
                            _context.Productos.Update(prod);
                        }
                    }
                }

                // =========================================================
                // 2. LIBERACIÓN DE PERFILES Y ELIMINACIÓN DE SUSCRIPCIONES
                // =========================================================
                var suscripciones = await _context.Suscripciones
                    .Where(s => s.IdVenta == id)
                    .ToListAsync();

                if (suscripciones.Any())
                {
                    var idsSuscripciones = suscripciones.Select(s => s.Id).ToList();

                    var renovacionesAsociadas = await _context.Renovaciones
                        .Where(r => idsSuscripciones.Contains(r.IdSuscripcion))
                        .ToListAsync();

                    if (renovacionesAsociadas.Any())
                    {
                        _context.Renovaciones.RemoveRange(renovacionesAsociadas);
                    }

                    foreach (var sus in suscripciones)
                    {
                        if (sus.IdPerfilCuenta.HasValue)
                        {
                            var perfil = await _context.PerfilesCuentas.FindAsync(sus.IdPerfilCuenta.Value);
                            if (perfil != null)
                            {
                                perfil.Ocupado = false;
                                perfil.IdClienteAsignado = null;
                                perfil.EstadoPerfil = "Disponible";
                                _context.PerfilesCuentas.Update(perfil);
                            }
                        }
                    }

                    _context.Suscripciones.RemoveRange(suscripciones);
                }

                // =========================================================
                // 3. LIMPIEZA CONTABLE (ARQUEO DE CAJA Y CUENTAS POR COBRAR)
                // =========================================================
                var movimientoCaja = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdVenta == id);
                if (movimientoCaja != null)
                {
                    _context.MovimientosCaja.Remove(movimientoCaja);
                }

                var cpc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.IdVenta == id);
                if (cpc != null)
                {
                    _context.CuentasPorCobrar.Remove(cpc);
                }

                // =========================================================
                // 4. ELIMINACIÓN DE DETALLES Y FACTURA
                // =========================================================
                _context.DetallesVentas.RemoveRange(venta.Detalles);
                _context.Ventas.Remove(venta);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = $"Factura #{id} eliminada e inventarios/cuentas revertidos con éxito." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al intentar eliminar la venta #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al cancelar y eliminar la factura." });
            }
        }
    }
}