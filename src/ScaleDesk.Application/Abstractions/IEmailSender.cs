namespace ScaleDesk.Application.Abstractions;

/// <summary>
/// Outbound email seam. The Application layer depends only on this; Infrastructure supplies an
/// implementation (a console/no-op sender in dev, a real provider later). Sending is best-effort:
/// a failed email must not roll back the transactional change that produced it.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
