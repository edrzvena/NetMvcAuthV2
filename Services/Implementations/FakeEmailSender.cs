using NetMvcAuthV2.Services.Interfaces;

namespace NetMvcAuthV2.Services.Implementations;

// Hanya untuk Development: email tidak dikirim, isinya ditulis ke log.
public class FakeEmailSender(ILogger<FakeEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        logger.LogInformation("[FakeEmailSender] To: {To} | Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
