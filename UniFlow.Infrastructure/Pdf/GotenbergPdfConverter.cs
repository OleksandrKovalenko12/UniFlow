using System.Net.Http.Headers;
using System.Text;

namespace UniFlow.Infrastructure.Pdf;

public sealed class GotenbergPdfConverter : IPdfConverter
{
    private const string LibreOfficeRoute = "forms/libreoffice/convert";
    private const string ChromiumHtmlRoute = "forms/chromium/convert/html";

    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string OdtContentType = "application/vnd.oasis.opendocument.text";
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";
    private const string WebPContentType = "image/webp";

    private const string PaperWidth = "21cm";
    private const string PaperHeight = "29.7cm";
    private const string Margin = "1cm";

    private readonly HttpClient _httpClient;

    public GotenbergPdfConverter(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<byte[]> ConvertToPdfAsync(Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var (route, form) = contentType switch
        {
            DocxContentType => (LibreOfficeRoute, CreateOfficeForm(content, contentType, "document.docx")),
            OdtContentType => (LibreOfficeRoute, CreateOfficeForm(content, contentType, "document.odt")),
            JpegContentType => (ChromiumHtmlRoute, CreateImageForm(content, contentType, "image.jpg")),
            PngContentType => (ChromiumHtmlRoute, CreateImageForm(content, contentType, "image.png")),
            WebPContentType => (ChromiumHtmlRoute, CreateImageForm(content, contentType, "image.webp")),
            _ => throw new ArgumentException(
                $"Content type '{contentType}' cannot be converted to PDF.", nameof(contentType))
        };

        using (form)
        {
            return await SendAsync(route, form, cancellationToken);
        }
    }

    private async Task<byte[]> SendAsync(string route, MultipartFormDataContent form,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsync(route, form, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PdfConversionException("Gotenberg is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PdfConversionException("Gotenberg request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new PdfConversionException(
                    $"Gotenberg returned {(int)response.StatusCode}: {error}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
    }

    private static MultipartFormDataContent CreateOfficeForm(Stream content, string contentType, 
        string filename)
    {
        var form = new MultipartFormDataContent();
        form.Add(CreateFileContent(content, contentType), "files", filename);
        return form;
    }

    private static MultipartFormDataContent CreateImageForm(Stream content, string contentType,
        string fileName)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8">
              <style>
                html, body { margin: 0; }
                img { display: block; margin: 0 auto; max-width: 100%; max-height: 99vh; }
              </style>
            </head>
            <body>
              <img src="{{fileName}}">
            </body>
            </html>
            """;

        var form = new MultipartFormDataContent();
        form.Add(new StringContent(html, Encoding.UTF8, "text/html"), "files", "index.html");
        form.Add(CreateFileContent(content, contentType), "files", fileName);
        form.Add(new StringContent(PaperWidth), "paperWidth");
        form.Add(new StringContent(PaperHeight), "paperHeight");

        foreach (var side in new[] { "marginTop", "marginBottom", "marginLeft", "marginRight" })
            form.Add(new StringContent(Margin), side);

        return form;
    }

    private static StreamContent CreateFileContent(Stream content, string contentType)
    {
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return file;
    }
}