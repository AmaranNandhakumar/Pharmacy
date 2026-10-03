namespace Pharmacy.Core.Entities;

/// <summary>Entered → Verified (by a pharmacist) → Dispensed, or Rejected with a reason.</summary>
public enum PrescriptionStatus
{
    Entered,
    Verified,
    Dispensed,
    Rejected
}
