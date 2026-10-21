namespace ScaleDesk.Api.Middleware;

/// <summary>
/// Baseline defensive HTTP headers (§ security). Applied at the very top of the pipeline and set
/// before the request is handled, so they are present on every response — including errors written
/// by the exception handler underneath. These are the standard browser hardening headers: no MIME
/// sniffing, no framing (clickjacking), a strict referrer policy, and a locked-down permissions
/// policy. HSTS is emitted only on TLS requests so plain-HTTP local/container traffic is unaffected.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=()";

        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await next(context);
    }
}
