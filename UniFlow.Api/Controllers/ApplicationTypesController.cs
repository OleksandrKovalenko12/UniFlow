using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared.ApplicationTypes;

namespace UniFlow.Api.Controllers;

// Any logged-in user (fallback policy)
[ApiController]
[Route("api/application-types")]
public sealed class ApplicationTypesController : ControllerBase
{
    private readonly UniFlowDbContext _db;

    public ApplicationTypesController(UniFlowDbContext db)
    {
        _db = db;
    }

    // All active types - Client shows only those it has a form for
    [HttpGet]
    public async Task<IReadOnlyList<ApplicationTypeSummary>> GetActive(
        CancellationToken cancellationToken) =>
        await _db.ApplicationTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new ApplicationTypeSummary(t.Id, t.Code, t.Name, t.Description))
            .ToListAsync(cancellationToken);
}