using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using UniFlow.Api.Errors;
using UniFlow.Domain;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Api;

public class GlobalExceptionHandlerTests
{
    // Runs the handler like the middleware does and returns the status and the JSON body
    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception,
        string environmentName)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            new HostingEnvironment { EnvironmentName = environmentName },
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        return (httpContext.Response.StatusCode, body);
    }

    [Fact]
    public async Task DomainException_WritesStatusAndCode()
    {
        var (status, body) = await HandleAsync(
            new DomainException(DomainErrorCode.InvalidStatus, "Wrong status."), Environments.Production);

        Assert.Equal(409, status);
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("InvalidStatus", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Production_DoesNotRevealExceptionMessage()
    {
        var (status, body) = await HandleAsync(
            new InvalidOperationException("Connection string: Host=secret"), Environments.Production);

        Assert.Equal(500, status);
        Assert.Equal("ServerError", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("detail", out _));
        Assert.DoesNotContain("secret", body.GetRawText());
    }

    [Fact]
    public async Task Development_IncludesExceptionMessageAsDetail()
    {
        var (_, body) = await HandleAsync(
            new ArgumentException("Bad value."), Environments.Development);

        Assert.Equal("Bad value.", body.GetProperty("detail").GetString());
    }
}