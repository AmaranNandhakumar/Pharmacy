namespace Pharmacy.Core.Entities;

/// <summary>
/// A row in the Schedule H1 or Schedule X register. Copied at sale time, so it stays correct even if
/// the patient or prescription is edited later. H1 registers are kept for 3 years.
/// </summary>
public class ScheduleRegisterEntry
{
    public long Id { get; set; }
    public DrugSchedule Schedule { get; set; }

    public int SaleItemId { get; set; }
    public SaleItem SaleItem { get; set; } = null!;

    public string InvoiceNo { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientAddress { get; set; }
    public string PrescriberName { get; set; } = string.Empty;
    public string PrescriberRegNo { get; set; } = string.Empty;
    public string? PrescriberAddress { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }

    public int PharmacistId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
