namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Validates and normalizes an uploaded image before it's ever written to disk. The client-
/// declared Content-Type header (checked by each upload command's FluentValidation rule) is
/// trivially spoofable - a renamed executable or a malicious file with a fake JPEG header
/// passes that check easily. This is the actual security boundary: it decodes the bytes with
/// a real image codec (which rejects anything that isn't a genuinely valid image) and
/// re-encodes to a canonical format, so what gets stored is provably an image, never a
/// pass-through of whatever the client uploaded.
/// </summary>
public interface IImageProcessor
{
    /// <summary>Decodes, validates, and re-encodes the given image content. Throws
    /// FluentValidation.ValidationException (already mapped to HTTP 400 by
    /// ExceptionHandlingMiddleware) if the content isn't a valid, safely-sized image.</summary>
    Task<Stream> ProcessAsync(Stream content, CancellationToken ct = default);
}
