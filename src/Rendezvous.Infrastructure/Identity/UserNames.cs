using System.Text.RegularExpressions;

namespace Rendezvous.Infrastructure.Identity;

public static class UserNames
{
    public const int MaxNameLength = 100;

    private static readonly Regex NamePattern = new(
        @"^[\p{L}\p{M}](?:[\p{L}\p{M}\p{Zs}'’.-]*[\p{L}\p{M}])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string value)
    {
        return value.Trim();
    }

    public static bool IsValidNamePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalizedValue = Normalize(value);

        return normalizedValue.Length <= MaxNameLength
            && NamePattern.IsMatch(normalizedValue);
    }

    public static string FormatFullName(string? firstName, string? lastName)
    {
        var normalizedFirstName = string.IsNullOrWhiteSpace(firstName)
            ? string.Empty
            : Normalize(firstName);
        var normalizedLastName = string.IsNullOrWhiteSpace(lastName)
            ? string.Empty
            : Normalize(lastName);

        return string.Join(
            " ",
            new[] { normalizedFirstName, normalizedLastName }
                .Where(name => name.Length > 0));
    }
}
