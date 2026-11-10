using UniFlow.Api.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Storage;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Infrastructure.Persistence.Seed;
using UniFlow.Infrastructure.Numbering;
using UniFlow.Api.Applications;
using UniFlow.Documents.Rendering;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UniFlow.Api.Controllers;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<UniFlowDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<UniFlowDbContext>()
    .AddSignInManager();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "UniFlow.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;

    // API: status codes instead of redirects to a login page
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// How often the cookie is checked against the user's security stamp (e.g. after deactivation)
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

// Secure by default: every endpoint requires login unless marked [AllowAnonymous]
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddScoped<DevelopmentSeeder>();
builder.Services.AddScoped<IApplicationNumberGenerator, ApplicationNumberGenerator>();

builder.Services.AddScoped<ApplicationSubmissionService>();
builder.Services.AddSingleton<DocumentRenderer>();

// Per account
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApplicationsController.SubmissionRateLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1)
            }));
});


var storageRoot = builder.Configuration["Storage:RootPath"]
    ?? throw new InvalidOperationException("Storage:RootPath is not configured.");
builder.Services.AddSingleton<IFileStorage>(
    new LocalFileStorage(Path.Combine(builder.Environment.ContentRootPath, storageRoot)));

var gotenbergUrl = builder.Configuration["Gotenberg:BaseUrl"]
    ?? throw new InvalidOperationException("Gotenberg:BaseUrl is not configured.");
builder.Services.AddHttpClient<IPdfConverter, GotenbergPdfConverter>(client =>
{
    client.BaseAddress = new Uri(gotenbergUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var seedPassword = app.Configuration["Seed:Password"]
        ?? throw new InvalidOperationException("Missing 'Seed:Password' in configuration.");

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
    await seeder.SeedAsync(seedPassword);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseWebAssemblyDebugging();
}

app.UseHttpsRedirection();

app.MapStaticAssets().AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
