namespace ScaleDesk.Domain.Constants;

/// <summary>
/// Fixed credentials for the public "Try the demo" path (Stage 5F.1). The password is never sent
/// to or from the client — the browser only calls <c>POST /api/auth/demo</c> and receives tokens.
/// The DbSeeder creates this tenant + user idempotently on startup, so it exists in every
/// environment without any additional setup.
/// </summary>
public static class DemoWorkspace
{
    public const string Slug = "demo";
    public const string Name = "ScaleDesk Demo";
    public const string OwnerEmail = "demo@scaledesk.app";
    public const string OwnerPassword = "DemoPass2026!";
    public const string AgentEmail = "agent@scaledesk.app";

    /// <summary>ScaleDesk HQ account (platform admin). Same dev-only password policy as the demo owner.</summary>
    public const string PlatformEmail = "platform@scaledesk.app";
}
