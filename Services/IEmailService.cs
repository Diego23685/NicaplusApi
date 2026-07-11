namespace NicaplusApi.Services
{
    public interface IEmailService
    {
        Task EnviarCorreoAsync(
            string para,
            string asunto,
            string html);
    }
}