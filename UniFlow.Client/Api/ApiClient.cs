using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Enrollments;

namespace UniFlow.Client.Api;

// The only place where pages talk to the server: every answer becomes an ApiResult
public sealed class ApiClient
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors =
        new Dictionary<string, string[]>();

    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;

    public ApiClient(HttpClient http, NavigationManager navigation)
    {
        _http = http;
        _navigation = navigation;
    }

    public Task<ApiResult<IReadOnlyList<ApplicationTypeSummary>>> GetApplicationTypesAsync() =>
        SendAsync<IReadOnlyList<ApplicationTypeSummary>>(() => _http.GetAsync("api/application-types"));

    public Task<ApiResult<IReadOnlyList<EnrollmentSummary>>> GetMyEnrollmentsAsync() =>
    SendAsync<IReadOnlyList<EnrollmentSummary>>(() => _http.GetAsync("api/enrollments/mine"));

    private async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        // No network or the request took too long
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Fail<T>(null, ApiErrorMessages.NoConnection, NoFieldErrors);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return ApiResult<T>.Success((await response.Content.ReadFromJsonAsync<T>())!);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // The session has expired: reload the page, the router sees no user and
                // sends them to the login page, which brings them back here afterwards
                _navigation.NavigateTo(_navigation.Uri, forceLoad: true);
                return Fail<T>(null, ApiErrorMessages.SessionExpired, NoFieldErrors);
            }

            var problem = await ReadProblemAsync(response);
            var fieldErrors = problem?.Errors ?? NoFieldErrors;
            var message = ApiErrorMessages.For(response.StatusCode, problem?.Code, fieldErrors.Count > 0);
            return Fail<T>(problem?.Code, message, fieldErrors);
        }
    }

    private static ApiResult<T> Fail<T>(string? code, string message,
        IReadOnlyDictionary<string, string[]> fieldErrors) =>
        ApiResult<T>.Failure(new ApiError(code, message, fieldErrors));

    // Error body (ProblemDetails) - some answers, e.g. 429 from the rate limiter, have none
    private static async Task<ProblemResponse?> ReadProblemAsync(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.Contains("json"))
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemResponse>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Only the parts of ProblemDetails the Client uses
    private sealed class ProblemResponse
    {
        public string? Code { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}