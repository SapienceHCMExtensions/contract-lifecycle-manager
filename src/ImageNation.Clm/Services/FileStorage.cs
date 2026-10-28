namespace ImageNation.Clm.Services;

public sealed record StoredFile(string RelativePath, string FileName);

public class FileStorage
{
    private readonly string _root;
    public const long MaxBytes = 15 * 1024 * 1024;

    public FileStorage(IWebHostEnvironment env, IConfiguration config)
    {
        var configured = config["Storage:UploadsPath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : configured;
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(IFormFile file, Guid contractId)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("The file is empty.");
        }

        if (file.Length > MaxBytes)
        {
            throw new InvalidOperationException("Files must be 15 MB or smaller.");
        }

        var safe = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "upload.bin";
        }

        var storedName = $"{Guid.NewGuid():N}_{safe}";
        var relative = Path.Combine(contractId.ToString(), storedName);
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var stream = File.Create(full);
        await file.CopyToAsync(stream);
        return new StoredFile(relative.Replace('\\', '/'), safe);
    }

    public async Task<StoredFile> SaveTextAsync(Guid contractId, string fileName, string content)
    {
        var safe = Path.GetFileName(fileName);
        var storedName = $"{Guid.NewGuid():N}_{safe}";
        var relative = Path.Combine(contractId.ToString(), storedName);
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content);
        return new StoredFile(relative.Replace('\\', '/'), safe);
    }

    public async Task<StoredFile> CopyAsync(Guid contractId, string relativeSource, string newFileName)
    {
        var source = Resolve(relativeSource);
        var safe = Path.GetFileName(newFileName);
        var storedName = $"{Guid.NewGuid():N}_{safe}";
        var relative = Path.Combine(contractId.ToString(), storedName);
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var input = File.OpenRead(source);
        await using var output = File.Create(full);
        await input.CopyToAsync(output);
        return new StoredFile(relative.Replace('\\', '/'), safe);
    }

    public string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        var rootFull = Path.GetFullPath(_root);
        if (!full.StartsWith(rootFull, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("That file path is not in the upload store.");
        }

        return full;
    }
}
