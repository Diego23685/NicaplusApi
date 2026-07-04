using System.Text.Json.Serialization;

namespace NicaplusApi.DTOs
{
    public class LoginDto
    {
        [JsonPropertyName("username")] // Forza a mapear desde la minúscula del Frontend
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")] // Forza a mapear desde la minúscula del Frontend
        public string Password { get; set; } = string.Empty;
    }
}