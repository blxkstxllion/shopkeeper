namespace ShopKeeper.Infrastructure.Storage;

using FluentValidation;
using FluentValidation.Results;
using SkiaSharp;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Real image validation, not a Content-Type header check - see IImageProcessor's doc comment
/// for why that distinction matters. Every upload (product photo, profile photo, business
/// logo) goes through this same instance, so there's exactly one security boundary to get
/// right instead of three copies that could drift.
/// </summary>
public class SkiaImageProcessor : IImageProcessor
{
    // Generous enough for any real photo (most phone cameras cap out around 4000-8000px on
    // the long edge) while still rejecting a "small file, absurd claimed dimensions"
    // decompression-bomb-style image - SKCodec.Info reads this from the header alone, before
    // any pixel data is decoded, so the check happens before the expensive allocation, not after.
    private const int MaxDimensionPixels = 6000;

    public async Task<Stream> ProcessAsync(Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var codec = SKCodec.Create(buffer);
        if (codec is null)
        {
            throw Invalid("The uploaded file isn't a valid image.");
        }

        if (codec.Info.Width > MaxDimensionPixels || codec.Info.Height > MaxDimensionPixels)
        {
            throw Invalid($"Images must be {MaxDimensionPixels}x{MaxDimensionPixels} pixels or smaller.");
        }

        buffer.Position = 0;
        using var bitmap = SKBitmap.Decode(buffer);
        if (bitmap is null)
        {
            throw Invalid("The uploaded file isn't a valid image.");
        }

        // Re-encoded to one canonical format regardless of what was uploaded - what's stored
        // is always a PNG this processor itself just generated from decoded pixel data, never
        // a pass-through of the original bytes. PNG specifically (not JPEG) because it's
        // lossless and supports transparency, which business logos commonly need. An animated
        // GIF flattens to its first frame - an accepted trade-off for none of these three
        // upload types (product photo, profile photo, logo) needing animation.
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        var output = new MemoryStream();
        encoded.SaveTo(output);
        output.Position = 0;
        return output;
    }

    private static ValidationException Invalid(string message) =>
        new([new ValidationFailure("File", message)]);
}
