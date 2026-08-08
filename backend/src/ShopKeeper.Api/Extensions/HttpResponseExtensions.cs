namespace ShopKeeper.Api.Extensions;

public static class HttpResponseExtensions
{
    private const string RefreshTokenCookieName = "shopkeeper_refresh_token";

    public static void SetRefreshTokenCookie(this HttpResponse response, string refreshToken, IWebHostEnvironment env)
    {
        response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/api/auth",
        });
    }

    public static void ClearRefreshTokenCookie(this HttpResponse response)
    {
        response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/auth" });
    }

    public static string? GetRefreshTokenCookie(this HttpRequest request) =>
        request.Cookies.TryGetValue(RefreshTokenCookieName, out var value) ? value : null;
}
