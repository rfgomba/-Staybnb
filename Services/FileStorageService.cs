namespace Staybnb.Services;

public interface IFileStorageService
{
    bool IsValidImage(IFormFile file, out string error);
    bool IsValidDocument(IFormFile file, out string error);

    // Public files (property photos) live in wwwroot and are served directly
    Task<string> SaveAsync(IFormFile file, string folder);
    void Delete(string relativeUrl);

    // Private files (guest ID documents) live outside wwwroot and are only served through an authorised action
    Task<string> SavePrivateAsync(IFormFile file, string folder);
    string GetPrivatePath(string folder, string fileName);
    void DeletePrivate(string folder, string fileName);
}

public class FileStorageService : IFileStorageService
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] DocumentExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
    private const long MaxBytes = 5 * 1024 * 1024; // 5MB

    private readonly IWebHostEnvironment _env;
    public FileStorageService(IWebHostEnvironment env) => _env = env;

    public bool IsValidImage(IFormFile file, out string error) =>
        Check(file, ImageExtensions, "JPG, PNG or WEBP images", out error);

    public bool IsValidDocument(IFormFile file, out string error) =>
        Check(file, DocumentExtensions, "JPG, PNG, WEBP or PDF files", out error);

    public async Task<string> SaveAsync(IFormFile file, string folder)
    {
        var name = await WriteAsync(file, Path.Combine(_env.WebRootPath, "uploads", folder));
        return $"/uploads/{folder}/{name}";
    }

    public void Delete(string relativeUrl)
    {
        var path = Path.Combine(_env.WebRootPath, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) File.Delete(path);
    }

    public Task<string> SavePrivateAsync(IFormFile file, string folder) => WriteAsync(file, PrivateRoot(folder));

    // Path.GetFileName stops "..\" path tricks
    public string GetPrivatePath(string folder, string fileName) =>
        Path.Combine(PrivateRoot(folder), Path.GetFileName(fileName));

    public void DeletePrivate(string folder, string fileName)
    {
        var path = GetPrivatePath(folder, fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    private string PrivateRoot(string folder) => Path.Combine(_env.ContentRootPath, "App_Data", "uploads", folder);

    private static async Task<string> WriteAsync(IFormFile file, string directory)
    {
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = new FileStream(Path.Combine(directory, name), FileMode.Create);
        await file.CopyToAsync(stream);
        return name;
    }

    private static bool Check(IFormFile file, string[] allowed, string label, out string error)
    {
        error = string.Empty;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
        {
            error = $"{file.FileName}: only {label} are allowed.";
            return false;
        }
        if (file.Length == 0 || file.Length > MaxBytes)
        {
            error = $"{file.FileName}: files must be under 5MB.";
            return false;
        }
        return true;
    }
}
