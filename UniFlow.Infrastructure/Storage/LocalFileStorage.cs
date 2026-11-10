namespace UniFlow.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private const int MaxExtensionLength = 10;
    private const int BufferSize = 81920;

    private readonly string _root;

    public LocalFileStorage(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }

    public async Task<string> SaveAsync(Stream content, string folder, string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ValidateExtension(extension);

        var relativePath = $"{folder}/{Guid.NewGuid():N}{extension}";
        var fullPath = GetSafeFullPath(relativePath, nameof(folder));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, BufferSize, useAsync: true);

        try
        {
            await using (file)
            {
                await content.CopyToAsync(file, cancellationToken);
            }
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }

        return relativePath;
    }

    public Stream OpenRead(string relativePath)
    {
        var fullPath = GetSafeFullPath(relativePath, nameof(relativePath));

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, BufferSize, useAsync: true);
    }

    public void Delete(string relativePath)
    {
        var fullPath = GetSafeFullPath(relativePath, nameof(relativePath));

        if (!File.Exists(fullPath))
            return;

        File.Delete(fullPath);
        RemoveFolderIfEmpty(Path.GetDirectoryName(fullPath)!);
    }

    private void RemoveFolderIfEmpty(string folder)
    {
        if (folder == _root)
            return;

        try
        {
            // Not recursive: a folder that still holds files is refused and stays
            Directory.Delete(folder);
        }
        catch (IOException)
        {
        }
    }

    private string GetSafeFullPath(string relativePath, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath, paramName);

        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));

        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Path must stay inside the storage root.", paramName);

        return fullPath;
    }

    private static void ValidateExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)
            || extension.Length < 2
            || extension.Length > MaxExtensionLength
            || extension[0] != '.'
            || !extension.Skip(1).All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException(
                "Extension must start with '.' followed by letters or digits.", nameof(extension));
    }
}