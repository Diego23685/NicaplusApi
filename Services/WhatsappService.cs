using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Services
{
    public interface IWhatsAppService
    {
        Task EnviarMensajeAsync(string telefono, string mensaje);
        Task EnviarDesdePlantillaAsync(string tipoDisparador, string telefono, Dictionary<string, string> variables);
    }

    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;

        public WhatsAppService(HttpClient httpClient, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
        }

        public async Task EnviarMensajeAsync(string telefono, string mensaje)
        {
            try
            {
                // Limpiar número nicaragüense si viene sin código de área
                string numeroDestino = telefono.Replace(" ", "").Replace("-", "");
                if (!numeroDestino.StartsWith("505")) numeroDestino = "505" + numeroDestino;

                // Ejemplo estructurado básico para una API externa (sustituir URL/Token por la real de tu proveedor)
                var payload = new { to = numeroDestino, message = mensaje };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "TU_WHATSAPP_TOKEN");
                await _httpClient.PostAsync("https://api.tuproveedorwpp.com/send", content);
            }
            catch (Exception ex)
            {
                // Fallar en silencio o loguear para no romper la experiencia del usuario de la app principal
                Console.WriteLine($"Error enviando WhatsApp: {ex.Message}");
            }
        }

        public async Task EnviarDesdePlantillaAsync(string tipoDisparador, string telefono, Dictionary<string, string> variables)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Buscar la plantilla configurada por el usuario en la DB
            var config = await context.ConfiguracionesMensajes
                .FirstOrDefaultAsync(c => c.TipoDisparador == tipoDisparador && c.Activo);

            if (config == null) return; // Si está desactivada o no existe, no hace nada

            string mensajeProcesado = config.PlantillaTexto;
            foreach (var variable in variables)
            {
                mensajeProcesado = mensajeProcesado.Replace($"{{{variable.Key}}}", variable.Value);
            }

            await EnviarMensajeAsync(telefono, mensajeProcesado);
        }
    }
}