using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;

namespace UniFlow.Infrastructure.Persistence.Seed;

public class DevelopmentSeeder
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public DevelopmentSeeder(UniFlowDbContext db, UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task SeedAsync(string password)
    {
        await _db.Database.MigrateAsync();

        await SeedRolesAsync();

        // Staff
        await EnsureUserAsync("adam.nowak@uniflow.test", "Adam", "Nowak", password,
            RoleNames.Administrator);
        var annaKowalska = await EnsureUserAsync("anna.kowalska@uniflow.test", "Anna", "Kowalska",
            password, RoleNames.DeanOffice);
        var katarzynaMazur = await EnsureUserAsync("katarzyna.mazur@uniflow.test", "Katarzyna",
            "Mazur", password, RoleNames.DeanOffice);
        var piotrWisniewski = await EnsureUserAsync("piotr.wisniewski@uniflow.test", "Piotr",
            "Wiśniewski", password, RoleNames.ViceDean);
        var ewaDabrowska = await EnsureUserAsync("ewa.dabrowska@uniflow.test", "Ewa", "Dąbrowska",
            password, RoleNames.DeanOffice);
        var marekKaminski = await EnsureUserAsync("marek.kaminski@uniflow.test", "Marek",
            "Kamiński", password, RoleNames.ViceDean);
        var joannaSzymanska = await EnsureUserAsync("joanna.szymanska@uniflow.test", "Joanna",
            "Szymańska", password, RoleNames.DeanOffice);

        // Students
        var janZielinski = await EnsureUserAsync("s100001@student.uniflow.test", "Jan",
            "Zieliński", password, RoleNames.Student);
        var mariaLewandowska = await EnsureUserAsync("s100002@student.uniflow.test", "Maria",
            "Lewandowska", password, RoleNames.Student);
        var tomaszWojcik = await EnsureUserAsync("s100003@student.uniflow.test", "Tomasz",
            "Wójcik", password, RoleNames.Student, RoleNames.DeanOffice);
        var pawelKrawczyk = await EnsureUserAsync("s100004@student.uniflow.test", "Paweł",
            "Krawczyk", password, RoleNames.Student);

        if (await _db.Faculties.AnyAsync())
            return;

        // Faculties and fields of study
        var wiit = new Faculty("WIiT", "Wydział Informatyki i Telekomunikacji");
        var wpia = new Faculty("WPiA", "Wydział Prawa i Administracji");

        var informatics1 = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, wiit);
        var informatics2 = new FieldOfStudy("Informatyka", StudyLevel.SecondCycle, wiit);
        var teleinformatics = new FieldOfStudy("Teleinformatyka", StudyLevel.FirstCycle, wiit);
        var law = new FieldOfStudy("Prawo", StudyLevel.LongCycle, wpia);
        var administration = new FieldOfStudy("Administracja", StudyLevel.FirstCycle, wpia);

        // Staff assignments
        var assignments = new[]
        {
            new StaffAssignment(annaKowalska.Id, wiit),
            new StaffAssignment(katarzynaMazur.Id, wiit),
            new StaffAssignment(piotrWisniewski.Id, wiit),
            new StaffAssignment(tomaszWojcik.Id, wiit),
            new StaffAssignment(ewaDabrowska.Id, wpia),
            new StaffAssignment(marekKaminski.Id, wpia),
            new StaffAssignment(joannaSzymanska.Id, wiit),
            new StaffAssignment(joannaSzymanska.Id, wpia),
        };

        // Student profiles and enrollments
        var profiles = new[]
        {
            new StudentProfile(janZielinski.Id, "100001", "jan.zielinski@example.com"),
            new StudentProfile(mariaLewandowska.Id, "100002"),
            new StudentProfile(tomaszWojcik.Id, "100003"),
            new StudentProfile(pawelKrawczyk.Id, "100004"),
        };

        var pawelEnrollment = new Enrollment(pawelKrawczyk.Id, administration, 4, StudyMode.FullTime);
        pawelEnrollment.Deactivate();

        var enrollments = new[]
        {
            new Enrollment(janZielinski.Id, informatics1, 3, StudyMode.FullTime),
            new Enrollment(mariaLewandowska.Id, informatics2, 1, StudyMode.FullTime),
            new Enrollment(mariaLewandowska.Id, law, 5, StudyMode.PartTime),
            new Enrollment(tomaszWojcik.Id, teleinformatics, 5, StudyMode.PartTime),
            pawelEnrollment,
        };

        // Application types
        var applicationTypes = new[]
        {
            new ApplicationType("PERSONAL_DATA_CHANGE", "Zmiana danych osobowych",
                "Zmiana imienia lub nazwiska w danych uczelni. Dołącz dokument potwierdzający " +
                "zmianę, np. odpis aktu małżeństwa lub decyzję o zmianie nazwiska.",
                requiresDeanDecision: false, maxAttachments: 2),
            new ApplicationType("GROUP_CHANGE", "Zmiana grupy zajęciowej",
                "Prośba o przeniesienie do innej grupy zajęciowej z wybranego przedmiotu. " +
                "Podaj przedmiot, obecną i oczekiwaną grupę oraz uzasadnienie. Możesz dołączyć " +
                "np. zaświadczenie od pracodawcy.",
                requiresDeanDecision: true, maxAttachments: 2),
            new ApplicationType("DEAN_LEAVE", "Urlop dziekański",
                "Wniosek o urlop od zajęć: zdrowotny, okolicznościowy lub wychowawczy. Przy " +
                "urlopie zdrowotnym dołącz dokumentację medyczną.",
                requiresDeanDecision: true, maxAttachments: 5),
        };

        _db.Faculties.AddRange(wiit, wpia);
        _db.FieldsOfStudy.AddRange(informatics1, informatics2, teleinformatics, law, administration);
        _db.StaffAssignments.AddRange(assignments);
        _db.StudentProfiles.AddRange(profiles);
        _db.Enrollments.AddRange(enrollments);
        _db.ApplicationTypes.AddRange(applicationTypes);

        await _db.SaveChangesAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in RoleNames.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, string firstName,
        string lastName, string password, params string[] roles)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
        };

        var created = await _userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException($"Seeding user {email} failed: " +
                string.Join("; ", created.Errors.Select(e => e.Description)));

        var rolesAdded = await _userManager.AddToRolesAsync(user, roles);
        if (!rolesAdded.Succeeded)
            throw new InvalidOperationException($"Assigning roles to {email} failed: " +
                string.Join("; ", rolesAdded.Errors.Select(e => e.Description)));

        return user;
    }
}