using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Identity;

namespace UniFlow.Infrastructure.Persistence;

public class UniFlowDbContext : IdentityDbContext<ApplicationUser>
{
    public UniFlowDbContext(DbContextOptions<UniFlowDbContext> options) : base(options) { }

    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<FieldOfStudy> FieldsOfStudy => Set<FieldOfStudy>();
    public DbSet<ApplicationType> ApplicationTypes => Set<ApplicationType>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(UniFlowDbContext).Assembly);
    }
}