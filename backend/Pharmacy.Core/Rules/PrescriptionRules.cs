using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Rules;

/// <summary>The prescription workflow: Entered → Verified → Dispensed (then refills), or Rejected.</summary>
public static class PrescriptionRules
{
    /// <summary>Returns an error message, or null if the medicine can go on a prescription.</summary>
    public static string? ValidateItemMedicine(Medicine medicine)
    {
        if (!medicine.IsActive) return $"{medicine.Name} is no longer active.";
        if (medicine.Schedule == DrugSchedule.Ndps) return $"{medicine.Name} is an NDPS drug; this app doesn't dispense those.";
        return null;
    }

    public static void Verify(Prescription rx, int pharmacistId, DateTime now)
    {
        if (rx.Status != PrescriptionStatus.Entered)
            throw new PrescriptionRuleException($"Only an entered prescription can be verified; this one is {rx.Status}.");
        if (!rx.CopyRetained && rx.Items.Any(i => i.Medicine.Schedule == DrugSchedule.X))
            throw new PrescriptionRuleException("Schedule X needs the duplicate copy of the prescription kept by the pharmacy.");

        rx.Status = PrescriptionStatus.Verified;
        rx.VerifiedById = pharmacistId;
        rx.VerifiedAt = now;
    }

    public static void Reject(Prescription rx, string reason)
    {
        if (rx.Status is not (PrescriptionStatus.Entered or PrescriptionStatus.Verified))
            throw new PrescriptionRuleException($"A {rx.Status} prescription can't be rejected.");

        rx.Status = PrescriptionStatus.Rejected;
        rx.RejectReason = reason;
    }

    /// <summary>
    /// The items to hand over now. A verified prescription is filled in full; a dispensed one is a refill,
    /// covering only the items that still have refills left.
    /// </summary>
    public static IReadOnlyList<PrescriptionItem> ItemsToFill(Prescription rx) => rx.Status switch
    {
        PrescriptionStatus.Verified => rx.Items.ToList(),
        PrescriptionStatus.Dispensed when rx.Items.Any(i => i.HasRefillsLeft) => rx.Items.Where(i => i.HasRefillsLeft).ToList(),
        PrescriptionStatus.Dispensed => throw new PrescriptionRuleException("This prescription has no refills left."),
        _ => throw new PrescriptionRuleException($"A prescription must be verified before it is dispensed; this one is {rx.Status}.")
    };

    /// <summary>Records a fill of <paramref name="items"/> (from <see cref="ItemsToFill"/>) on the prescription.</summary>
    public static void RecordFill(Prescription rx, IReadOnlyList<PrescriptionItem> items, int pharmacistId, DateTime now)
    {
        var isRefill = rx.Status == PrescriptionStatus.Dispensed;
        foreach (var item in items)
        {
            if (isRefill) item.RefillsUsed++;
            item.QuantityDispensed += item.Quantity;
        }

        rx.Status = PrescriptionStatus.Dispensed;
        rx.DispensedById = pharmacistId;
        rx.DispensedAt = now;
    }
}

public class PrescriptionRuleException : Exception
{
    public PrescriptionRuleException(string message) : base(message) { }
}
