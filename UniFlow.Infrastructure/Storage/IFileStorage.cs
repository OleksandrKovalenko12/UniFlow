namespace UniFlow.Infrastructure.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string folder, string extension,
        CancellationToken cancellationToken = default);
    Stream OpenRead(string relativePath);
    void Delete(string relativePath);
}