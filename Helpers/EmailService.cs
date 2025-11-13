using MailKit.Net.Smtp;
using MimeKit;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress("Your App Name", _configuration["Email:From"]));
        emailMessage.To.Add(new MailboxAddress("", to));
        emailMessage.Subject = subject;
        emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html)
        {
            Text = body
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_configuration["Email:SmtpHost"], int.Parse(_configuration["Email:SmtpPort"]), true);
        await client.AuthenticateAsync(_configuration["Email:Username"], _configuration["Email:Password"]);
        await client.SendAsync(emailMessage);
        await client.DisconnectAsync(true);
    }
}
