namespace ShopKeeper.Api.Extensions;

public static class HttpResponseExtensions
{
    private const string RefreshTokenCookieName = "shopkeeper_refresh_token";

    /// <summary>`persistent` mirrors the user's "Keep me signed in" choice (AuthResultDto.RememberMe):
    /// true sets an explicit Expires so the cookie survives a browser restart; false omits it
    /// entirely so the browser treats it as a session cookie, cleared when the browser fully closes.</summary>
    public static void SetRefreshTokenCookie(this HttpResponse response, string refreshToken, IWebHostEnvironment env, bool persistent)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
        };

        if (persistent)
        {
            options.Expires = DateTimeOffset.UtcNow.AddDays(30);
        }

        response.Cookies.Append(RefreshTokenCookieName, refreshToken, options);
    }

    public static void ClearRefreshTokenCookie(this HttpResponse response)
    {
        response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/auth" });
    }

    public static string? GetRefreshTokenCookie(this HttpRequest request) =>
        request.Cookies.TryGetValue(RefreshTokenCookieName, out var value) ? value : null;
}
