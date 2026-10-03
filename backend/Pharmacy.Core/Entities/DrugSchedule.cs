namespace Pharmacy.Core.Entities;

/// <summary>Drug classification under India's Drugs and Cosmetics Rules 1945 (plus the NDPS Act).</summary>
public enum DrugSchedule
{
    /// <summary>Over the counter: no prescription needed.</summary>
    Otc,
    /// <summary>Schedule G: sold with a caution label; no prescription needed.</summary>
    G,
    /// <summary>Schedule H: prescription only.</summary>
    H,
    /// <summary>Schedule H1: prescription only, and each sale goes in the H1 register.</summary>
    H1,
    /// <summary>Schedule X: prescription in duplicate, one copy kept, separate register.</summary>
    X,
    /// <summary>Narcotic or psychotropic (NDPS Act). The MVP tracks stock but blocks sale.</summary>
    Ndps
}
