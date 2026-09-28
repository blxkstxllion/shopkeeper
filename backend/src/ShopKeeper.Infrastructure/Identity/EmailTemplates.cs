namespace ShopKeeper.Infrastructure.Identity;

using System.Net;

/// <summary>Shared HTML/text body builder for every real email sender (SesEmailSender,
/// ResendEmailSender) - one visual template regardless of which provider is actually
/// delivering the message, so switching providers never means re-designing the emails.</summary>
internal static class EmailTemplates
{
    public static (string Html, string Text) Build(string? firstName, string message, string ctaLabel, string ctaLink)
    {
        var greeting = string.IsNullOrWhiteSpace(firstName) ? "Hi," : $"Hi {WebUtility.HtmlEncode(firstName)},";
        var html = $"""
            <p>{greeting}</p>
            <p>{message}</p>
            <p><a href="{ctaLink}" style="display:inline-block;padding:10px 20px;background:#16a34a;color:#fff;border-radius:8px;text-decoration:none;">{ctaLabel}</a></p>
            <p style="color:#666;font-size:13px;">If the button doesn't work, copy and paste this link into your browser:<br />{ctaLink}</p>
            """;
        var text = $"{greeting}\n\n{StripHtml(message)}\n\n{ctaLabel}: {ctaLink}";
        return (html, text);
    }

    private static string StripHtml(string value) => value.Replace("<strong>", "").Replace("</strong>", "");
}
