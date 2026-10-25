namespace Staybnb.Services;

public interface IFileStorageService
{
    bool IsValidImage(IFormFile file, out string error);
    Task<string> SaveAsync(IFormFile file, string folder);
    void Delete(string relativeUrl);
}

/// <summary>Saves uploads under wwwroot/uploads/{folder} and returns the public URL.</summary>
public class FileStorageService : IFileStorageService
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5MB

    private readonly IWebHostEnvironment _env;
    public FileStorageService(IWebHostEnvironment env) => _env = env;

    public bool IsValidImage(IFormFile file, out string error)
    {
        error = string.Empty;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!ImageExtensions.Contains(ext))
        {
            error = $"{file.FileName}: only JPG, PNG or WEBP images are allowed.";
            return false;
        }
        if (file.Length == 0 || file.Length > MaxImageBytes)
        {
            error = $"{file.FileName}: each image must be under 5MB.";
            return false;
        }
        return true;
    }

    public async Task<string> SaveAsync(IFormFile file, string folder)
    {
        var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{folder}/{fileName}";
    }

    public void Delete(string relativeUrl)
    {
        var path = Path.Combine(_env.WebRootPath,
            relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) File.Delete(path);
    }
}
