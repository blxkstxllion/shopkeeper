namespace ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Object-storage abstraction (section 28 of the product spec: never store large files
/// directly in Postgres). The current implementation writes to local disk, which is a real,
/// working choice for a single-server deployment - not a stand-in. Swapping in S3/Azure
/// Blob/Cloudinary later only touches the Infrastructure implementation, never callers.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Saves the stream under the given folder and returns a URL the frontend can load directly.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default);
}
