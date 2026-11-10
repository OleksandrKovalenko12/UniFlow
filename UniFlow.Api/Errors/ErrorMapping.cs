using Microsoft.EntityFrameworkCore;
using UniFlow.Domain;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Pdf;

namespace UniFlow.Api.Errors;

// Exception -> HTTP status + error code; the Client turns the code into a Polish message
public static class ErrorMapping
{
    public const string InvalidArgument = "InvalidArgument";
    public const string ConcurrencyConflict = "ConcurrencyConflict";
    public const string PdfServiceUnavailable = "PdfServiceUnavailable";
    public const string ServerError = "ServerError";

    public static (int Status, string Code) Map(Exception exception) => exception switch
    {
        DomainException domain => (StatusFor(domain.Code), domain.Code.ToString()),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ConcurrencyConflict),
        PdfConversionException => (StatusCodes.Status503ServiceUnavailable, PdfServiceUnavailable),
        ArgumentException => (StatusCodes.Status400BadRequest, InvalidArgument),
        _ => (StatusCodes.Status500InternalServerError, ServerError)
    };

    private static int StatusFor(DomainErrorCode code) => code switch
    {
        // Who is acting
        DomainErrorCode.RoleNotAllowed
            or DomainErrorCode.NotApplicant
            or DomainErrorCode.ConflictOfInterest
            or DomainErrorCode.NotOwnEnrollment => StatusCodes.Status403Forbidden,

        DomainErrorCode.AttachmentNotFound
            or DomainErrorCode.EnrollmentNotFound => StatusCodes.Status404NotFound,

        // State of the application, enrollment or attachment
        DomainErrorCode.InvalidStatus
            or DomainErrorCode.EnrollmentInactive
            or DomainErrorCode.ApplicationTypeInactive
            or DomainErrorCode.DeanDecisionRequired
            or DomainErrorCode.DeanDecisionNotRequired
            or DomainErrorCode.AttachmentLimitReached
            or DomainErrorCode.AttachmentNotArchivable
            or DomainErrorCode.AttachmentAlreadyArchived
            or DomainErrorCode.EnrollmentAlreadyInactive
            or DomainErrorCode.EnrollmentAlreadyActive => StatusCodes.Status409Conflict,

        // New code without a decision - a test catches it
        _ => StatusCodes.Status500InternalServerError
    };
}