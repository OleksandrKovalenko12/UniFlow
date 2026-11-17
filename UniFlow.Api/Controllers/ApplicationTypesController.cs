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

    // All active types - Client shows only those it has a form for.
    // all: switched-off ones too - the dean's office still filters the applications of a kind
    // that can no longer be submitted.
    [HttpGet]
    public async Task<IReadOnlyList<ApplicationTypeSummary>> GetActive(bool all = false,
        CancellationToken cancellationToken = default) =>
        await _db.ApplicationTypes
            .Where(t => all || t.IsActive)
            .OrderBy(t => t.Name)
             .Select(t => new ApplicationTypeSummary(t.Id, t.Code, t.Name, t.Description, t.MaxAttachments))
            .ToListAsync(cancellationToken);
}