using System.Net.Mail;

namespace UniFlow.Domain.Entities;

public class StudentProfile
{
    private const int MinAlbumNumberLength = 4;
    private const int MaxAlbumNumberLength = 10;

    public string UserId { get; private set; } = null!;
    public string AlbumNumber { get; private set; } = null!;
    public string? PrivateEmail { get; private set; }

    public StudentProfile(string userId, string albumNumber, string? privateEmail = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        AlbumNumber = ValidateAlbumNumber(albumNumber);
        UserId = userId;
        PrivateEmail = NormalizeEmail(privateEmail);
    }

    private StudentProfile() { }

    public void ChangePrivateEmail(string? newEmail)
    {
        PrivateEmail = NormalizeEmail(newEmail);
    }

    private static string ValidateAlbumNumber(string albumNumber)
    {
        if (string.IsNullOrEmpty(albumNumber)
            || albumNumber.Length < MinAlbumNumberLength
            || albumNumber.Length > MaxAlbumNumberLength
            || !albumNumber.All(char.IsAsciiDigit))
        {
            throw new ArgumentException($"Album number must contain {MinAlbumNumberLength} - {MaxAlbumNumberLength} digits",
                nameof(albumNumber));
        }

        return albumNumber;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var trimmed = email.Trim();

        if (!MailAddress.TryCreate(trimmed, out var address) ||
                address.Address != trimmed)
                throw new ArgumentException("Invalid e-mail address.", nameof(email));

        return trimmed;
    }

}
