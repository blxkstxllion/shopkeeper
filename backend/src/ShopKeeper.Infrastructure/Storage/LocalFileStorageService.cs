namespace ShopKeeper.Infrastructure.Storage;

using Microsoft.Extensions.Hosting;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Writes uploads under {ContentRoot}/App_Data/uploads/{folder}/, served back out at the
/// "/uploads" request path (mapped in Program.cs). Kept outside wwwroot deliberately - this
/// is a Web API project with no wwwroot by default, and App_Data reads clearly as
/// "persisted app data", not "static site assets".
/// </summary>
public class LocalFileStorageService(IHostEnvironment env) : IFileStorageService
{
    private const string UploadsRootFolderName = "App_Data/uploads";

    public async Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
    {
        var folderPath = Path.Combine(env.ContentRootPath, UploadsRootFolderName, folder);
        Directory.CreateDirectory(folderPath);

        var fullPath = Path.Combine(folderPath, fileName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        return $"/uploads/{folder}/{fileName}";
    }
}
