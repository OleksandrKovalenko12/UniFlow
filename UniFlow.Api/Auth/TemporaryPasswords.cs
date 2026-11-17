using System.Security.Cryptography;

namespace UniFlow.Api.Auth;

// A temporary password for a new account or after a reset. The administrator passes it on,
// the user replaces it at the first login.
public static class TemporaryPasswords
{
    // Without look-alikes (I/l/1, O/0) - it is read out or copied by hand
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%&*?";
    private const string All = Upper + Lower + Digits + Symbols;

    public const int Length = 12;

    // One character of each kind, so the password always meets the password rules
    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);

        for (var i = 4; i < Length; i++)
            chars[i] = Pick(All);

        // Otherwise the kinds would always stand in the same places
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    // A cryptographic generator - Random could be predicted
    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}