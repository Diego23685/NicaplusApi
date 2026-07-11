namespace NicaplusApi.DTOs.Clientes
{
    public class ClienteAuthResponseDto
    {
        public string Token { get; set; } = string.Empty;

        public ClientePerfilDto Cliente { get; set; } = new();
    }
}