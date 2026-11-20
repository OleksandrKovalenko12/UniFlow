using Microsoft.EntityFrameworkCore;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared.Forms;

namespace UniFlow.Api.Applications;

public static class PersonalDataQueries
{
    // What the system knows about the student now: the name from the account, the private
    // e-mail from the profile. Used by the personal data application and the demo data.
    public static Task<CurrentPersonalData> CurrentPersonalDataAsync(this UniFlowDbContext db,
        string userId, CancellationToken cancellationToken) =>
        db.Users
            .Where(u => u.Id == userId)
            .Select(u => new CurrentPersonalData(u.FirstName, u.LastName,
                db.StudentProfiles.Where(p => p.UserId == u.Id).Select(p => p.PrivateEmail).FirstOrDefault()))
            .SingleAsync(cancellationToken);
}