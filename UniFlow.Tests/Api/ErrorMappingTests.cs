using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Errors;
using UniFlow.Domain;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Pdf;

namespace UniFlow.Tests.Api;

public class ErrorMappingTests
{
    private static DomainException Domain(DomainErrorCode code) => new(code, "Rule broken.");

    //Domain errors

    [Theory]
    [InlineData(DomainErrorCode.RoleNotAllowed)]
    [InlineData(DomainErrorCode.NotApplicant)]
    [InlineData(DomainErrorCode.ConflictOfInterest)]
    [InlineData(DomainErrorCode.NotOwnEnrollment)]
    public void Map_WhoIsActingErrors_Return403(DomainErrorCode code)
    {
        Assert.Equal((StatusCodes.Status403Forbidden, code.ToString()), ErrorMapping.Map(Domain(code)));
    }

    [Fact]
    public void Map_AttachmentNotFound_Returns404()
    {
        Assert.Equal((StatusCodes.Status404NotFound, "AttachmentNotFound"),
            ErrorMapping.Map(Domain(DomainErrorCode.AttachmentNotFound)));
    }

    [Theory]
    [InlineData(DomainErrorCode.InvalidStatus)]
    [InlineData(DomainErrorCode.EnrollmentInactive)]
    [InlineData(DomainErrorCode.ApplicationTypeInactive)]
    [InlineData(DomainErrorCode.DeanDecisionRequired)]
    [InlineData(DomainErrorCode.DeanDecisionNotRequired)]
    [InlineData(DomainErrorCode.AttachmentLimitReached)]
    [InlineData(DomainErrorCode.AttachmentNotArchivable)]
    [InlineData(DomainErrorCode.AttachmentAlreadyArchived)]
    [InlineData(DomainErrorCode.EnrollmentAlreadyInactive)]
    [InlineData(DomainErrorCode.EnrollmentAlreadyActive)]
    public void Map_StateErrors_Return409(DomainErrorCode code)
    {
        Assert.Equal((StatusCodes.Status409Conflict, code.ToString()), ErrorMapping.Map(Domain(code)));
    }

    // A new DomainErrorCode must get a conscious decision in ErrorMapping
    [Fact]
    public void Map_EveryDomainErrorCode_HasClientErrorStatus()
    {
        foreach (var code in Enum.GetValues<DomainErrorCode>())
        {
            var (status, _) = ErrorMapping.Map(Domain(code));

            Assert.True(status is 403 or 404 or 409, $"{code} has no HTTP status in ErrorMapping.");
        }
    }

    //Other exceptions

    [Fact]
    public void Map_ConcurrencyException_Returns409()
    {
        Assert.Equal((StatusCodes.Status409Conflict, ErrorMapping.ConcurrencyConflict),
            ErrorMapping.Map(new DbUpdateConcurrencyException()));
    }

    [Fact]
    public void Map_PdfConversionException_Returns503()
    {
        Assert.Equal((StatusCodes.Status503ServiceUnavailable, ErrorMapping.PdfServiceUnavailable),
            ErrorMapping.Map(new PdfConversionException("Gotenberg is unreachable.")));
    }

    [Fact]
    public void Map_ArgumentExceptions_Return400()
    {
        var expected = (StatusCodes.Status400BadRequest, ErrorMapping.InvalidArgument);

        Assert.Equal(expected, ErrorMapping.Map(new ArgumentException("Bad.")));
        Assert.Equal(expected, ErrorMapping.Map(new ArgumentNullException("value")));
        Assert.Equal(expected, ErrorMapping.Map(new ArgumentOutOfRangeException("value")));
    }

    [Fact]
    public void Map_UnknownException_Returns500()
    {
        var expected = (StatusCodes.Status500InternalServerError, ErrorMapping.ServerError);

        Assert.Equal(expected, ErrorMapping.Map(new InvalidOperationException("Boom.")));
        Assert.Equal(expected, ErrorMapping.Map(new NullReferenceException()));
    }
}