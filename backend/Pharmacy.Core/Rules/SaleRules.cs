using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Rules;

public static class SaleRules
{
    /// <summary>Highest counter discount; bigger discounts need a manager outside the app.</summary>
    public const decimal MaxDiscountPercent = 20m;

    /// <summary>
    /// OTC and Schedule G medicines can be sold straight off the shelf. Everything else
    /// (H, H1, X) is billed only through a dispensed prescription, and NDPS isn't sold at all.
    /// </summary>
    public static bool CanSellOverTheCounter(Medicine medicine) =>
        medicine.Schedule is DrugSchedule.Otc or DrugSchedule.G;

    /// <summary>Schedules whose sales are copied into a register.</summary>
    public static bool NeedsRegisterEntry(DrugSchedule schedule) =>
        schedule is DrugSchedule.H1 or DrugSchedule.X;
}
