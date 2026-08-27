using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UploadsController> _logger;
        private readonly string[] _extensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };

        public UploadsController(IWebHostEnvironment environment, ILogger<UploadsController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpPost("producto")]
        [Authorize]
        [RequestSizeLimit(25 * 1024 * 1024)] // Límite de 25 MB
        public async Task<IActionResult> SubirImagenProducto([FromForm] IFormFile? archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(new { mensaje = "No se ha seleccionado ningún archivo válido." });
                }

                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!_extensionesPermitidas.Contains(extension))
                {
                    return BadRequest(new { mensaje = "Formato de imagen no permitido. Use JPG, PNG o WEBP." });
                }

                // Ruta: wwwroot/uploads/products
                string wwwrootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string carpetaDestino = Path.Combine(wwwrootPath, "uploads", "products");

                if (!Directory.Exists(carpetaDestino))
                {
                    Directory.CreateDirectory(carpetaDestino);
                }

                // Generar nombre de archivo único
                string nombreArchivo = $"prod_{Guid.NewGuid()}{extension}";
                string rutaCompleta = Path.Combine(carpetaDestino, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                // Generar URL completa pública
                string host = Request.Host.Value ?? "localhost";
                string scheme = Request.Scheme; // http o https
                string urlRelativa = $"/uploads/products/{nombreArchivo}";
                string urlCompleta = $"{scheme}://{host}{urlRelativa}";

                return Ok(new
                {
                    mensaje = "Imagen subida correctamente.",
                    url = urlCompleta,
                    nombreArchivo = nombreArchivo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen física de producto.");
                return StatusCode(500, new { mensaje = "Error interno al guardar la imagen en el servidor." });
            }
        }
    }
}