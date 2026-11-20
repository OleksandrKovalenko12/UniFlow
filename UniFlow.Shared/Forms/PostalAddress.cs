using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace UniFlow.Shared.Forms;

// A correspondence address, in Poland or abroad - the old and the new one look the same
public sealed partial class PostalAddress
{
    public const int MaxStreetLength = 150;
    public const int MaxCityLength = 100;
    public const int MaxCountryLength = 100;
    public const string DefaultCountry = "Polska";

    public string? Street { get; set; } // with the number, e.g. "ul. Kwiatowa 5/12"
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(PostalCode)
        && string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(Country);

    public void Normalize()
    {
        Street = Street?.Trim();
        PostalCode = PostalCode?.Trim();
        City = City?.Trim();
        Country = Country?.Trim();
    }

    // prefix: where the fields are in the form, e.g. "NewAddress."
    public IEnumerable<ValidationResult> Validate(string prefix)
    {
        if (string.IsNullOrWhiteSpace(Street))
            yield return Error("Podaj ulicę i numer.", prefix + nameof(Street));
        else if (Street.Trim().Length > MaxStreetLength)
            yield return Error($"Adres może mieć najwyżej {MaxStreetLength} znaków.", prefix + nameof(Street));

        if (string.IsNullOrWhiteSpace(PostalCode))
            yield return Error("Podaj kod pocztowy.", prefix + nameof(PostalCode));
        else if (!PostalCodeFormat().IsMatch(PostalCode.Trim()))
            yield return Error("Kod pocztowy może mieć 2-12 znaków: cyfry, litery, spacje i myślniki.",
                prefix + nameof(PostalCode));

        if (string.IsNullOrWhiteSpace(City))
            yield return Error("Podaj miejscowość.", prefix + nameof(City));
        else if (City.Trim().Length > MaxCityLength)
            yield return Error($"Miejscowość może mieć najwyżej {MaxCityLength} znaków.", prefix + nameof(City));

        if (string.IsNullOrWhiteSpace(Country))
            yield return Error("Podaj kraj.", prefix + nameof(Country));
        else if (Country.Trim().Length > MaxCountryLength)
            yield return Error($"Kraj może mieć najwyżej {MaxCountryLength} znaków.", prefix + nameof(Country));
    }

    // The same place, written the same way - capital letters and spaces around do not count
    public bool SameAs(PostalAddress other) =>
        Same(Street, other.Street) && Same(PostalCode, other.PostalCode)
        && Same(City, other.City) && Same(Country, other.Country);

    // "ul. Kwiatowa 5/12, 00-950 Lipowa Wola, Polska" - the PDF and the summary
    public override string ToString() => $"{Street}, {PostalCode} {City}, {Country}";

    private static bool Same(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    // Codes of many countries: "00-950", "10115", "SW1A 1AA" - something more than separators
    [GeneratedRegex(@"^(?=.*[A-Za-z0-9])[A-Za-z0-9 \-]{2,12}$")]
    private static partial Regex PostalCodeFormat();

    private static ValidationResult Error(string message, string memberName) =>
        new(message, [memberName]);
}