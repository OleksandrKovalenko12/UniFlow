using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Auth;
using UniFlow.Shared.Welcome;

namespace UniFlow.Tests.Infrastructure.Database;

// The start page: the previous login and a section for each role
[Collection("Postgres")]
public sealed class WelcomeTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public WelcomeTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<ApplicationUser> CreateUserAsync(string? password = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid():N}@uniflow.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Ola",
            LastName = "Testowa",
            CreatedAt = DateTime.UtcNow,
        };

        var created = password is null
            ? await users.CreateAsync(user)
            : await users.CreateAsync(user, password);
        Assert.True(created.Succeeded);
        return user;
    }

    // The second login remembers when the first one was - "since the previous login"
    [Fact]
    public async Task Login_Twice_KeepsThePreviousLoginTime()
    {
        const string password = "Test!Haslo9";
        var user = await CreateUserAsync(password);
        var login = new LoginRequest { Email = user.Email!, Password = password };

        (await _client.PostAsJsonAsync("/api/auth/login", login)).EnsureSuccessStatusCode();
        DateTime? first;
        await using (var db = _postgres.CreateContext())
            first = await db.Users.Where(u => u.Id == user.Id).Select(u => u.LastLoginAt).SingleAsync();

        (await _client.PostAsJsonAsync("/api/auth/login", login)).EnsureSuccessStatusCode();

        await using (var db = _postgres.CreateContext())
        {
            var times = await db.Users.Where(u => u.Id == user.Id)
                .Select(u => new { u.PreviousLoginAt, u.LastLoginAt })
                .SingleAsync();
            Assert.NotNull(first);
            Assert.Equal(first, times.PreviousLoginAt);
            Assert.True(times.LastLoginAt > times.PreviousLoginAt);
        }
    }

    // A student who is also the administrator - and nothing of the dean's office
    [Fact]
    public async Task Get_ShowsTheSectionsOfTheUsersRolesOnly()
    {
        var user = await CreateUserAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/welcome");
        request.Headers.Add(TestAuthHandler.UserHeader, user.Id);
        request.Headers.Add(TestAuthHandler.RolesHeader, $"{RoleNames.Student},{RoleNames.Administrator}");

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var welcome = (await response.Content.ReadFromJsonAsync<WelcomeDetails>())!;
        Assert.Equal("Ola", welcome.FirstName);
        Assert.Null(welcome.PreviousLoginAt);
        Assert.NotNull(welcome.Student);
        Assert.NotNull(welcome.Admin);
        Assert.Null(welcome.DeanOffice);
        Assert.Null(welcome.ViceDean);
    }
}