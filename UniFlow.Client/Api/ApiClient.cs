using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Enrollments;
using UniFlow.Shared.Applications;
using UniFlow.Shared.Forms;
using UniFlow.Client.Applications;
using UniFlow.Shared.Review;

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

    public Task<ApiResult<SubmittedApplication>> SubmitGroupChangeAsync(int enrollmentId,
    Guid submissionKey, GroupChangeForm form) =>
    SendAsync<SubmittedApplication>(() =>
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/enrollments/{enrollmentId}/applications/group-change")
        {
            Content = JsonContent.Create(form)
        };
        request.Headers.Add(SubmissionHeaders.IdempotencyKey, submissionKey.ToString());
        return _http.SendAsync(request);
    });

    public Task<ApiResult<PagedList<ReviewListItem>>> GetReviewListAsync(ReviewTab tab,
    string? search, string? type, int page) =>
    SendAsync<PagedList<ReviewListItem>>(() => _http.GetAsync(
        $"api/review?tab={tab}&page={page}" +
        (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search)}") +
        (string.IsNullOrWhiteSpace(type) ? "" : $"&type={Uri.EscapeDataString(type)}")));

    public Task<ApiResult<ReviewCounts>> GetReviewCountsAsync() =>
        SendAsync<ReviewCounts>(() => _http.GetAsync("api/review/counts"));

    public Task<ApiResult<IReadOnlyList<MyApplicationItem>>> GetMyApplicationsAsync() =>
    SendAsync<IReadOnlyList<MyApplicationItem>>(() => _http.GetAsync("api/applications/mine"));

    public Task<ApiResult<ApplicationDetails>> WithdrawAsync(Guid applicationId) =>
        SendAsync<ApplicationDetails>(() =>
            _http.PostAsync($"api/applications/{applicationId}/withdraw", content: null));

    public Task<ApiResult<ApplicationDetails>> GetApplicationAsync(Guid id) =>
        SendAsync<ApplicationDetails>(() => _http.GetAsync($"api/applications/{id}"));

    // A decision returns the application as it is now - the page shows it at once
    public Task<ApiResult<ApplicationDetails>> DecideAsync(Guid applicationId, ReviewAction action,
        string? comment) =>
        SendAsync<ApplicationDetails>(() => _http.PostAsJsonAsync(
            $"api/applications/{applicationId}/{DecisionPath(action)}", new { comment }));

    private static string DecisionPath(ReviewAction action) => action switch
    {
        ReviewAction.Take => "take",
        ReviewAction.RequestCorrection => "request-correction",
        ReviewAction.ForwardToDean => "forward",
        ReviewAction.Approve => "approve",
        ReviewAction.Reject => "reject",
        ReviewAction.ReturnToDeanOffice => "return",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown action.")
    };

    // One file per request; the server checks it, converts it to PDF and returns its id
    public Task<ApiResult<AttachmentSummary>> UploadAttachmentAsync(Guid applicationId,
        PickedFile file) =>
        SendAsync<AttachmentSummary>(() =>
        {
            var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(file.Content), "file", file.Name }
            };
            return _http.PostAsync($"api/applications/{applicationId}/attachments", content);
        });

    public static string AttachmentUrl(Guid applicationId, Guid attachmentId, bool inline) =>
        $"api/applications/{applicationId}/attachments/{attachmentId}" +
        (inline ? "?inline=true" : "");

    // A link, not a request: the browser opens or saves the PDF itself (and sends the login cookie)
    public static string DocumentUrl(Guid applicationId, bool inline) =>
        $"api/applications/{applicationId}/document" + (inline ? "?inline=true" : "");
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