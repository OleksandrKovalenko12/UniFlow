using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Admin;

namespace UniFlow.Tests.Infrastructure.Database;

// The administrator's accounts: what is saved, what is logged, what is not allowed
[Collection("Postgres")]
public sealed class AdminAccountsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;
    private string _adminId = null!;

    public AdminAccountsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // The test database has no seed - the roles are created here
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in RoleNames.All)
                if (!await roles.RoleExistsAsync(role))
                    await roles.CreateAsync(new IdentityRole(role));
        }

        // The acting administrator must exist - the audit log points to them
        await using var db = _postgres.CreateContext();
        _adminId = Guid.NewGuid().ToString("N");
        db.Users.Add(new ApplicationUser
        {
            Id = _adminId,
            UserName = _adminId,
            Email = $"{_adminId}@test.local",
            FirstName = "Adam",
            LastName = "Admin",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        request.Headers.Add(TestAuthHandler.UserHeader, _adminId);
        request.Headers.Add(TestAuthHandler.RolesHeader, RoleNames.Administrator);
        return _client.SendAsync(request);
    }

    private async Task<Faculty> CreateFacultyAsync()
    {
        await using var db = _postgres.CreateContext();
        var code = Guid.NewGuid().ToString("N")[..8];
        var faculty = new Faculty(code, $"Wydział {code}");
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync();
        return faculty;
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@uniflow.test";

    [Fact]
    public async Task Create_Student_SavesProfileRolesAndLog_WithATemporaryPassword()
    {
        var album = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var form = new AccountForm
        {
            FirstName = " Ola ",
            LastName = "Nowa",
            Email = UniqueEmail(),
            Roles = [RoleNames.Student],
            AlbumNumber = album,
        };

        var response = await SendAsync(HttpMethod.Post, "/api/admin/accounts", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issued = (await response.Content.ReadFromJsonAsync<IssuedPassword>())!;

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(issued.AccountId))!;

        Assert.Equal("Ola", user.FirstName);
        Assert.True(user.MustChangePassword);
        Assert.True(await users.CheckPasswordAsync(user, issued.Password));
        Assert.Equal([RoleNames.Student], await users.GetRolesAsync(user));

        await using var db = _postgres.CreateContext();
        Assert.True(await db.StudentProfiles.AnyAsync(p => p.UserId == user.Id && p.AlbumNumber == album));
        var logged = await db.AuditLogEntries.Where(e => e.TargetUserId == user.Id)
            .Select(e => e.Action).ToListAsync();
        Assert.Contains(AuditAction.UserCreated, logged);
        Assert.Contains(AuditAction.RoleAssigned, logged);
    }

    [Fact]
    public async Task Update_RemovingTheStaffRole_RemovesFacultiesAndLogsIt()
    {
        var faculty = await CreateFacultyAsync();
        var form = new AccountForm
        {
            FirstName = "Ewa",
            LastName = "Pracownik",
            Email = UniqueEmail(),
            Roles = [RoleNames.DeanOffice],
            FacultyIds = [faculty.Id],
        };
        var created = await SendAsync(HttpMethod.Post, "/api/admin/accounts", form);
        var id = (await created.Content.ReadFromJsonAsync<IssuedPassword>())!.AccountId;

        form.Roles = [RoleNames.Student];
        form.AlbumNumber = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var response = await SendAsync(HttpMethod.Put, $"/api/admin/accounts/{id}", form);

        response.EnsureSuccessStatusCode();
        var details = (await response.Content.ReadFromJsonAsync<AccountDetails>())!;
        Assert.Equal([RoleNames.Student], details.Roles);
        Assert.Empty(details.FacultyIds);

        await using var db = _postgres.CreateContext();
        var logged = await db.AuditLogEntries.Where(e => e.TargetUserId == id)
            .Select(e => e.Action).ToListAsync();
        Assert.Contains(AuditAction.RoleRemoved, logged);
        Assert.Contains(AuditAction.StaffUnassigned, logged);
    }

    [Fact]
    public async Task Deactivate_OwnAccount_IsForbidden()
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/admin/accounts/{_adminId}/deactivate");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var db = _postgres.CreateContext();
        Assert.True(await db.Users.Where(u => u.Id == _adminId).Select(u => u.IsActive).SingleAsync());
    }
}