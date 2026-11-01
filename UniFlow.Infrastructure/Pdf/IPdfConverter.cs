namespace UniFlow.Infrastructure.Pdf;

public interface IPdfConverter
{
    Task<byte[]> ConvertToPdfAsync(Stream content, string contentType,
        CancellationToken cancellationToken = default);
}