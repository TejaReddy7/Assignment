using IplStore.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.Services;

/// <summary>Dev email sender: logs to console. Replace with SendGrid/SMTP in production.</summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("📧 [Email] To: {To} | Subject: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
