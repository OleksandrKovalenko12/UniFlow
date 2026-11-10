using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using UniFlow.Shared.Auth;

namespace UniFlow.Client.Auth;

// The login cookie is HttpOnly, so the browser code cannot read it.
// Who is logged in is therefore asked from the server and remembered for the session.
public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly HttpClient _http;
    private Task<AuthenticationState>? _state;

    public CookieAuthenticationStateProvider(HttpClient http)
    {
        _http = http;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        _state ??= LoadStateAsync();

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/auth/login", request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return LoginResult.InvalidCredentials;

            if (!response.IsSuccessStatusCode)
                return LoginResult.ServerError;

            var user = await response.Content.ReadFromJsonAsync<CurrentUser>();
            _state = Task.FromResult(ToState(user!));
            NotifyAuthenticationStateChanged(_state);
            return LoginResult.Success;
        }
        catch (HttpRequestException)
        {
            return LoginResult.ServerError;
        }
    }

    private async Task<AuthenticationState> LoadStateAsync()
    {
        try
        {
            using var response = await _http.GetAsync("api/auth/me");
            if (!response.IsSuccessStatusCode)
                return Anonymous;

            var user = await response.Content.ReadFromJsonAsync<CurrentUser>();
            return ToState(user!);
        }
        catch (HttpRequestException)
        {
            return Anonymous;
        }
    }

    private static AuthenticationState ToState(CurrentUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Passing the authentication type is what makes IsAuthenticated true
        var identity = new ClaimsIdentity(claims, authenticationType: "Cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}