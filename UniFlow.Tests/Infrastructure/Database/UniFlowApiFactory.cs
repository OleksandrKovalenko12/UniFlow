using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Shared;

namespace UniFlow.Tests.Infrastructure.Database;

// The real API on a test PostgreSQL; only Gotenberg and the login are replaced
public sealed class UniFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FakePdfConverter Pdf { get; } = new();

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "uniflow-tests", Guid.NewGuid().ToString("N"));

    public UniFlowApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Storage:RootPath", StorageRoot);
        builder.UseSetting("Gotenberg:BaseUrl", "http://localhost:1/");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPdfConverter>();
            services.AddSingleton<IPdfConverter>(Pdf);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}

public sealed class FakePdfConverter : IPdfConverter
{
    public bool Fail { get; set; }

    public Task<byte[]> ConvertToPdfAsync(Stream content, string contentType,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<byte[]> ConvertHtmlToPdfAsync(string html, CancellationToken cancellationToken = default) =>
        Fail
            ? throw new PdfConversionException("Gotenberg is down.")
            : Task.FromResult("%PDF-test"u8.ToArray());
}

// Logs in the user named in the request header, as a student
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, RoleNames.Student),
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}