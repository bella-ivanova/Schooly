namespace StudyAssistant.Services;

public interface IEmailService
{
    Task SendPasswordResetCodeAsync(string toEmail, string code);
}
