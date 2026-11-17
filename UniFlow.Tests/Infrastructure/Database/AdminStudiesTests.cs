using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Admin;

namespace UniFlow.Tests.Infrastructure.Database;

// A student's studies, managed by the administrator
[Collection("Postgres")]
public sealed class AdminStudiesTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;
    private string _studentId = null!;
    private FieldOfStudy _field = null!;

    public AdminStudiesTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // The test database has no seed - a student with the role and a field of study
        using var scope = _factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(RoleNames.Student))
            await roles.CreateAsync(new IdentityRole(RoleNames.Student));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var student = new ApplicationUser
        {
            UserName = $"{Guid.NewGuid():N}@uniflow.test",
            Email = $"{Guid.NewGuid():N}@uniflow.test",
            FirstName = "Jan",
            LastName = "Student",
            CreatedAt = DateTime.UtcNow,
        };
        await users.CreateAsync(student);
        await users.AddToRoleAsync(student, RoleNames.Student);
        _studentId = student.Id;

        await using var db = _postgres.CreateContext();
        var code = Guid.NewGuid().ToString("N")[..8];
        _field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, new Faculty(code, $"Wydział {code}"));
        db.FieldsOfStudy.Add(_field);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // The administrator is not looked up - studies are not in the audit log
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"/api/admin/accounts/{_studentId}/studies{url}");
        if (body is not null)
            request.Content = JsonContent.Create(body);
        request.Headers.Add(TestAuthHandler.UserHeader, "admin");
        request.Headers.Add(TestAuthHandler.RolesHeader, RoleNames.Administrator);
        return _client.SendAsync(request);
    }

    private async Task<StudiesItem> AddAsync()
    {
        var response = await SendAsync(HttpMethod.Post, "", new AddStudiesRequest
        {
            FieldOfStudyId = _field.Id,
            Semester = 3,
            StudyMode = StudyModeOption.PartTime
        });
        response.EnsureSuccessStatusCode();
        return Assert.Single((await response.Content.ReadFromJsonAsync<List<StudiesItem>>())!);
    }

    [Fact]
    public async Task EndAndResume_KeepTheSameStudies()
    {
        var added = await AddAsync();
        Assert.True(added.IsActive);
        Assert.Equal(StudyModeOption.PartTime, added.StudyMode);
        Assert.Equal(8, added.MaxSemester);

        (await SendAsync(HttpMethod.Post, $"/{added.Id}/end")).EnsureSuccessStatusCode();
        var response = await SendAsync(HttpMethod.Post, $"/{added.Id}/resume",
            new ResumeStudiesRequest { Semester = 4 });

        response.EnsureSuccessStatusCode();
        var resumed = Assert.Single((await response.Content.ReadFromJsonAsync<List<StudiesItem>>())!);
        Assert.Equal(added.Id, resumed.Id);
        Assert.True(resumed.IsActive);
        Assert.Equal(4, resumed.Semester);
    }

    [Fact]
    public async Task Add_FieldOfEndedStudies_SaysToResumeThem()
    {
        var added = await AddAsync();
        await SendAsync(HttpMethod.Post, $"/{added.Id}/end");

        var response = await SendAsync(HttpMethod.Post, "", new AddStudiesRequest
        {
            FieldOfStudyId = _field.Id,
            Semester = 1,
            StudyMode = StudyModeOption.FullTime
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("wznów", Assert.Single(problem!.Errors[nameof(AddStudiesRequest.FieldOfStudyId)]));
    }
}