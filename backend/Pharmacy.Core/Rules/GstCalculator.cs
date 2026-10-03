namespace Pharmacy.Core.Rules;

/// <summary>The money on one invoice line, in rupees.</summary>
public record GstLine(decimal Gross, decimal Discount, decimal TaxableValue, decimal Cgst, decimal Sgst, decimal Total);

/// <summary>
/// Works GST out backwards from a GST-inclusive price, as on Indian retail bills:
/// taxable value = total × 100 / (100 + rate), and the GST is split equally into CGST and SGST
/// (intra-state sale). Every amount is rounded to the paisa, and the parts always add up to the total.
/// </summary>
public static class GstCalculator
{
    public static GstLine Calculate(decimal unitPrice, int quantity, decimal gstRatePercent, decimal discountPercent)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Price can't be negative.");
        if (discountPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount must be 0 to 100%.");

        var gross = Round(unitPrice * quantity);
        var discount = Round(gross * discountPercent / 100m);
        var total = gross - discount;
        var taxable = Round(total * 100m / (100m + gstRatePercent));
        var gst = total - taxable;
        var cgst = Round(gst / 2m);
        var sgst = gst - cgst;

        return new GstLine(gross, discount, taxable, cgst, sgst, total);
    }

    /// <summary>
    /// Indian financial year label for a date, e.g. 3 Oct 2026 → "2026-27" and 15 Feb 2027 → "2026-27".
    /// </summary>
    public static string FinancialYear(DateOnly date)
    {
        var start = date.Month >= 4 ? date.Year : date.Year - 1;
        return $"{start}-{(start + 1) % 100:00}";
    }

    public static string InvoiceNo(string financialYear, int sequence) => $"{financialYear}/{sequence:000000}";

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
