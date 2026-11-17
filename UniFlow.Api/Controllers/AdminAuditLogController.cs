using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Time;
using UniFlow.Shared;
using UniFlow.Shared.Admin;
using UniFlow.Shared.Review;

namespace UniFlow.Api.Controllers;

// The audit log: who changed permissions, personal data or the structure, and when.
// Read only - nobody edits or deletes entries, not even the administrator.
[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = RoleNames.Administrator)]
public sealed class AdminAuditLogController : ControllerBase
{
    public const int PageSize = 25;

    private readonly UniFlowDbContext _db;

    public AdminAuditLogController(UniFlowDbContext db)
    {
        _db = db;
    }

    // search: part of the name or e-mail of the account an entry is about. account: one account
    // ("Historia zmian" on its page). from, to: days in Polish time, both included.
    [HttpGet]
    public async Task<PagedList<AuditLogItem>> GetList(string? search = null, AuditGroup? group = null,
        string? account = null, DateOnly? from = null, DateOnly? to = null, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(account))
            query = query.Where(e => e.TargetUserId == account);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e => _db.Users.Any(u => u.Id == e.TargetUserId
                && ((u.FirstName + " " + u.LastName).ToLower().Contains(term)
                    || u.Email!.ToLower().Contains(term))));
        }

        if (group is not null)
        {
            var actions = AuditGroups.KindsOf(group.Value).Select(kind => (AuditAction)kind).ToList();
            query = query.Where(e => actions.Contains(e.Action));
        }

        // The whole "to" day counts - up to the next midnight, Polish time
        if (from is not null)
        {
            var start = PolishTime.StartOfDayUtc(from.Value);
            query = query.Where(e => e.CreatedAt >= start);
        }

        if (to is not null)
        {
            var end = PolishTime.StartOfDayUtc(to.Value.AddDays(1));
            query = query.Where(e => e.CreatedAt < end);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        page = Math.Max(page, 1);

        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(e => new
            {
                e.Id,
                e.CreatedAt,
                ActorName = _db.Users.Where(u => u.Id == e.ActorId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                e.Action,
                e.TargetUserId,
                TargetName = _db.Users.Where(u => u.Id == e.TargetUserId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                e.Details
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AuditLogItem(r.Id, r.CreatedAt, r.ActorName ?? "", (AuditEntryKind)r.Action,
                r.TargetUserId, r.TargetName, r.Details))
            .ToList();

        return new PagedList<AuditLogItem>(items, totalCount, page, PageSize);
    }
}