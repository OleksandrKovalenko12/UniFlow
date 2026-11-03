using System.Net;
using UniFlow.Infrastructure.Pdf;

namespace UniFlow.Tests.Infrastructure;

public class GotenbergPdfConverterTests
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string OdtContentType = "application/vnd.oasis.opendocument.text";

    private static readonly Uri BaseAddress = new("http://gotenberg/");
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46];

    private static GotenbergPdfConverter CreateConverter(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = BaseAddress });

    private static FakeHandler PdfResponse() =>
        new(HttpStatusCode.OK, new ByteArrayContent(Pdf));

    private static Stream FileContent() => new MemoryStream("FILE-CONTENT"u8.ToArray());

    //Constructor

    [Fact]
    public void Constructor_NullHttpClient_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new GotenbergPdfConverter(null!));

        Assert.Equal("httpClient", ex.ParamName);
    }

    //Office documents

    [Theory]
    [InlineData(DocxContentType, "document.docx")]
    [InlineData(OdtContentType, "document.odt")]
    public async Task Convert_OfficeDocument_PostsToLibreOffice(string contentType, string fileName)
    {
        var handler = PdfResponse();

        var result = await CreateConverter(handler).ConvertToPdfAsync(FileContent(), contentType);

        Assert.Equal(Pdf, result);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(new Uri(BaseAddress, "forms/libreoffice/convert"), handler.Request.RequestUri);
        Assert.Contains($"filename={fileName}", handler.RequestBody);
        Assert.Contains($"Content-Type: {contentType}", handler.RequestBody);
        Assert.Contains("FILE-CONTENT", handler.RequestBody);
        Assert.DoesNotContain("index.html", handler.RequestBody);
    }

    //Images

    [Theory]
    [InlineData("image/jpeg", "image.jpg")]
    [InlineData("image/png", "image.png")]
    [InlineData("image/webp", "image.webp")]
    public async Task Convert_Image_PostsHtmlPageToChromium(string contentType, string fileName)
    {
        var handler = PdfResponse();

        var result = await CreateConverter(handler).ConvertToPdfAsync(FileContent(), contentType);

        Assert.Equal(Pdf, result);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(new Uri(BaseAddress, "forms/chromium/convert/html"), handler.Request.RequestUri);
        Assert.Contains("filename=index.html", handler.RequestBody);
        Assert.Contains($"<img src=\"{fileName}\">", handler.RequestBody);
        Assert.Contains($"filename={fileName}", handler.RequestBody);
        Assert.Contains($"Content-Type: {contentType}", handler.RequestBody);
        Assert.Contains("FILE-CONTENT", handler.RequestBody);
    }

    [Fact]
    public async Task Convert_Image_SendsA4PaperAndMargins()
    {
        var handler = PdfResponse();

        await CreateConverter(handler).ConvertToPdfAsync(FileContent(), "image/jpeg");

        var body = handler.RequestBody!;
        Assert.Contains("name=paperWidth\r\n\r\n21cm", body);
        Assert.Contains("name=paperHeight\r\n\r\n29.7cm", body);

        foreach (var side in new[] { "marginTop", "marginBottom", "marginLeft", "marginRight" })
            Assert.Contains($"name={side}\r\n\r\n1cm", body);
    }

    //HTML documents

    [Fact]
    public async Task ConvertHtml_PostsIndexHtmlToChromium()
    {
        var handler = PdfResponse();

        var result = await CreateConverter(handler).ConvertHtmlToPdfAsync("<p>Wniosek</p>");

        Assert.Equal(Pdf, result);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(new Uri(BaseAddress, "forms/chromium/convert/html"), handler.Request.RequestUri);
        Assert.Contains("filename=index.html", handler.RequestBody);
        Assert.Contains("Content-Type: text/html; charset=utf-8", handler.RequestBody);
        Assert.Contains("<p>Wniosek</p>", handler.RequestBody);
    }

    [Fact]
    public async Task ConvertHtml_SendsA4PaperAndDocumentMargins()
    {
        var handler = PdfResponse();

        await CreateConverter(handler).ConvertHtmlToPdfAsync("<p>Wniosek</p>");

        var body = handler.RequestBody!;
        Assert.Contains("name=paperWidth\r\n\r\n21cm", body);
        Assert.Contains("name=paperHeight\r\n\r\n29.7cm", body);
        Assert.Contains("name=marginTop\r\n\r\n2cm", body);
        Assert.Contains("name=marginBottom\r\n\r\n2cm", body);
        Assert.Contains("name=marginLeft\r\n\r\n2.5cm", body);
        Assert.Contains("name=marginRight\r\n\r\n2cm", body);
    }

    [Fact]
    public async Task ConvertHtml_KeepsPolishCharacters()
    {
        var handler = PdfResponse();

        await CreateConverter(handler).ConvertHtmlToPdfAsync("<p>Zażółć gęślą jaźń</p>");

        Assert.Contains("<p>Zażółć gęślą jaźń</p>", handler.RequestBody);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConvertHtml_EmptyHtml_Throws(string html)
    {
        var handler = PdfResponse();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateConverter(handler).ConvertHtmlToPdfAsync(html));

        Assert.Equal("html", ex.ParamName);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ConvertHtml_NullHtml_Throws()
    {
        var handler = PdfResponse();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateConverter(handler).ConvertHtmlToPdfAsync(null!));

        Assert.Equal("html", ex.ParamName);
        Assert.Null(handler.Request);
    }

    //Invalid input

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("image/heic")]
    [InlineData("text/plain")]
    public async Task Convert_UnsupportedContentType_ThrowsWithoutSending(string contentType)
    {
        var handler = PdfResponse();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateConverter(handler).ConvertToPdfAsync(FileContent(), contentType));

        Assert.Equal("contentType", ex.ParamName);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Convert_EmptyContentType_Throws(string contentType)
    {
        var handler = PdfResponse();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateConverter(handler).ConvertToPdfAsync(FileContent(), contentType));

        Assert.Equal("contentType", ex.ParamName);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Convert_NullContent_Throws()
    {
        var handler = PdfResponse();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateConverter(handler).ConvertToPdfAsync(null!, DocxContentType));

        Assert.Equal("content", ex.ParamName);
        Assert.Null(handler.Request);
    }

    //Gotenberg failures

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Convert_GotenbergReturnsError_ThrowsPdfConversionException(HttpStatusCode status)
    {
        var handler = new FakeHandler(status, new StringContent("conversion failed"));

        var ex = await Assert.ThrowsAsync<PdfConversionException>(
            () => CreateConverter(handler).ConvertToPdfAsync(FileContent(), DocxContentType));

        Assert.Contains(((int)status).ToString(), ex.Message);
        Assert.Contains("conversion failed", ex.Message);
    }

    [Fact]
    public async Task Convert_GotenbergUnreachable_ThrowsPdfConversionException()
    {
        var handler = new FakeHandler(exception: new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<PdfConversionException>(
            () => CreateConverter(handler).ConvertToPdfAsync(FileContent(), DocxContentType));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Convert_Timeout_ThrowsPdfConversionException()
    {
        var handler = new FakeHandler(exception: new TaskCanceledException());

        var ex = await Assert.ThrowsAsync<PdfConversionException>(
            () => CreateConverter(handler).ConvertToPdfAsync(FileContent(), DocxContentType));

        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    [Fact]
    public async Task Convert_CancelledByCaller_IsNotWrapped()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateConverter(PdfResponse())
                .ConvertToPdfAsync(FileContent(), DocxContentType, cts.Token));
    }

    //Stream ownership

    [Fact]
    public async Task Convert_DisposesInputStream()
    {
        var content = FileContent();

        await CreateConverter(PdfResponse()).ConvertToPdfAsync(content, DocxContentType);

        Assert.False(content.CanRead);
    }
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly HttpContent? _responseContent;
        private readonly Exception? _exception;

        public FakeHandler(HttpStatusCode status = HttpStatusCode.OK,
            HttpContent? responseContent = null, Exception? exception = null)
        {
            _status = status;
            _responseContent = responseContent;
            _exception = exception;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            if (_exception is not null)
                throw _exception;

            return new HttpResponseMessage(_status) { Content = _responseContent ?? new ByteArrayContent([]) };
        }
    }
}
