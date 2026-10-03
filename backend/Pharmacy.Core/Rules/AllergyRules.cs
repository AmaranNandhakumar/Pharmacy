using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Rules;

public static class AllergyRules
{
    private static readonly char[] Separators = { ',', ';', '\n' };

    /// <summary>Splits a patient's allergy text into individual terms ("Penicillin; sulfa" → penicillin, sulfa).</summary>
    public static IReadOnlyList<string> ParseAllergies(string? allergies) =>
        string.IsNullOrWhiteSpace(allergies)
            ? Array.Empty<string>()
            : allergies.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(a => a.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    /// <summary>
    /// The patient's allergy terms that appear in the medicine's brand or generic name. A simple text match:
    /// it catches "amoxicillin" for a penicillin-allergic patient only if the allergy is written as "amoxicillin",
    /// so it is a prompt for the pharmacist, not a substitute for their judgement.
    /// </summary>
    public static IReadOnlyList<string> FindMatches(string? allergies, Medicine medicine) =>
        ParseAllergies(allergies)
            .Where(term => Contains(medicine.Name, term) || Contains(medicine.GenericName, term))
            .ToList();

    private static bool Contains(string? text, string term) =>
        text is not null && text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
