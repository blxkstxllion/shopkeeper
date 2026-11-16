namespace ShopKeeper.Api.Local;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Offline-edition-only. LocalAuthenticationHandler authenticates every request as the owner
/// with no token check of its own (see its doc comment) - that's safe only as long as nothing
/// *other* than this app's own frontend can ever reach the sidecar. Binding to 127.0.0.1 blocks
/// other machines on the network, but NOT another origin running in the same browser on this
/// machine: this server's CORS policy is AllowAnyOrigin (needed because the Tauri webview's
/// origin isn't a plain http:// origin this API could allow-list), so without this middleware,
/// any website the shop owner happens to have open could fetch() this API and the browser would
/// let that page's JS read the response - full read/write of sales, customers, and financial
/// data, no PIN required. This is the actual authorization boundary: a random token generated
/// fresh every process launch (see Program.cs) and handed to the Tauri frontend out-of-band via
/// the sidecar's stdout - something a web page has no way to ever observe or guess.
/// </summary>
public class LocalApiTokenMiddleware(RequestDelegate next, string expectedToken)
{
    public const string HeaderName = "X-Local-Api-Token";

    public async Task InvokeAsync(HttpContext context)
    {
        // Exempted: the frontend's splash screen polls this before it necessarily has the token
        // yet (see offline-edition/api.ts's waitForBackend), and it reveals nothing sensitive -
        // "is the process accepting requests" (/health/live) or a bare health/unhealthy string
        // (/health/ready), same as the hosted SaaS build's equivalent endpoints.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();

        // FixedTimeEquals requires equal-length inputs to say anything at all about timing, but
        // there's no secret-dependent information in the length check itself here (the expected
        // token's length isn't sensitive) - using it anyway costs nothing and removes any doubt.
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var isValid = providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);

        if (!isValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
