using Microsoft.Extensions.Logging;
using ScaleDesk.Application.Abstractions;

namespace ScaleDesk.Infrastructure.Email;

/// <summary>
/// Development email sink: logs the message instead of sending it. Keeps the Application layer's
/// IEmailSender contract honest without external credentials, and makes invite links observable in
/// the console during local runs. Swap for a real provider (SES/SendGrid) by changing this binding.
/// </summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("DEV EMAIL to={To} subject={Subject} body={Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
