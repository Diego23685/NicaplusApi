using System.Net.Http;
using System.Text;
using System.Text.Json;
using NicaplusApi.Models.Brevo;

namespace NicaplusApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public EmailService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task EnviarCorreoAsync(
            string para,
            string asunto,
            string html)
        {
            var apiKey = _configuration["Brevo:ApiKey"];

            var remitente = new BrevoSender
            {
                Name = _configuration["Brevo:RemitenteNombre"]!,
                Email = _configuration["Brevo:RemitenteEmail"]!
            };

            var request = new BrevoEmailRequest
            {
                Sender = remitente,

                Subject = asunto,

                HtmlContent = html,

                To = new List<BrevoRecipient>
                {
                    new BrevoRecipient
                    {
                        Email = para
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);

            var contenido = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();

            _httpClient.DefaultRequestHeaders.Add(
                "api-key",
                apiKey);

            var respuesta = await _httpClient.PostAsync(
                "https://api.brevo.com/v3/smtp/email",
                contenido);

            if (!respuesta.IsSuccessStatusCode)
            {
                var error = await respuesta.Content.ReadAsStringAsync();

                throw new Exception(
                    $"Brevo devolvió un error: {error}");
            }
        }
    }
}