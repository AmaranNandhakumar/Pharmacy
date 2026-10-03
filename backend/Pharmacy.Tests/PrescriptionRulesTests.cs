using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;

namespace Pharmacy.Tests;

public class FefoAllocatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);

    private static Batch NewBatch(int id, int qty, DateOnly expiry) =>
        new() { Id = id, BatchNumber = $"B{id}", QuantityOnHand = qty, ExpiryDate = expiry };

    [Fact]
    public void Allocate_TakesEarliestExpiryFirst_AndSplitsAcrossBatches()
    {
        var later = NewBatch(1, 50, Today.AddMonths(12));
        var sooner = NewBatch(2, 8, Today.AddMonths(2));

        var picks = FefoAllocator.Allocate(new[] { later, sooner }, 10, Today);

        Assert.Equal(2, picks.Count);
        Assert.Same(sooner, picks[0].Batch);
        Assert.Equal(8, picks[0].Quantity);
        Assert.Same(later, picks[1].Batch);
        Assert.Equal(2, picks[1].Quantity);
    }

    [Fact]
    public void Allocate_SkipsExpiredAndEmptyBatches()
    {
        var expired = NewBatch(1, 100, Today.AddDays(-1));
        var empty = NewBatch(2, 0, Today.AddDays(10));
        var good = NewBatch(3, 5, Today.AddMonths(6));

        var pick = Assert.Single(FefoAllocator.Allocate(new[] { expired, empty, good }, 5, Today));

        Assert.Same(good, pick.Batch);
    }

    [Fact]
    public void Allocate_ExpiringToday_IsStillSellable()
    {
        var today = NewBatch(1, 5, Today);

        Assert.Single(FefoAllocator.Allocate(new[] { today }, 5, Today));
    }

    [Fact]
    public void Allocate_NotEnoughSellableStock_Throws()
    {
        var expired = NewBatch(1, 100, Today.AddDays(-1));
        var good = NewBatch(2, 3, Today.AddMonths(6));

        var ex = Assert.Throws<StockRuleException>(() => FefoAllocator.Allocate(new[] { expired, good }, 5, Today));
        Assert.Contains("Only 3 units", ex.Message);
    }

    [Fact]
    public void Allocate_DoesNotChangeBatches()
    {
        var batch = NewBatch(1, 10, Today.AddMonths(6));

        FefoAllocator.Allocate(new[] { batch }, 4, Today);

        Assert.Equal(10, batch.QuantityOnHand);
    }
}

public class AllergyRulesTests
{
    private static readonly Medicine Amoxicillin = new() { Name = "Mox 500", GenericName = "Amoxicillin" };

    [Theory]
    [InlineData("amoxicillin", "amoxicillin")]
    [InlineData("Sulfa; AMOXICILLIN", "AMOXICILLIN")]
    [InlineData("dust, mox", "mox")]
    public void FindMatches_MatchesBrandOrGenericName_CaseInsensitive(string allergies, string expected)
    {
        Assert.Equal(new[] { expected }, AllergyRules.FindMatches(allergies, Amoxicillin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("peanuts, latex")]
    [InlineData("ox")] // too short to be a meaningful term
    public void FindMatches_NoMatch_ReturnsEmpty(string? allergies)
    {
        Assert.Empty(AllergyRules.FindMatches(allergies, Amoxicillin));
    }
}

public class PrescriptionRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

    private static Prescription NewRx(DrugSchedule schedule = DrugSchedule.H, int refills = 0) => new()
    {
        Items = { new PrescriptionItem { Quantity = 10, RefillsAllowed = refills, Medicine = new Medicine { Name = "Drug", Schedule = schedule } } }
    };

    [Fact]
    public void Verify_EnteredPrescription_RecordsPharmacist()
    {
        var rx = NewRx();

        PrescriptionRules.Verify(rx, pharmacistId: 5, Now);

        Assert.Equal(PrescriptionStatus.Verified, rx.Status);
        Assert.Equal(5, rx.VerifiedById);
        Assert.Equal(Now, rx.VerifiedAt);
    }

    [Fact]
    public void Verify_Twice_Throws()
    {
        var rx = NewRx();
        PrescriptionRules.Verify(rx, 5, Now);

        Assert.Throws<PrescriptionRuleException>(() => PrescriptionRules.Verify(rx, 5, Now));
    }

    [Fact]
    public void Verify_ScheduleXWithoutRetainedCopy_Throws()
    {
        var rx = NewRx(DrugSchedule.X);

        Assert.Throws<PrescriptionRuleException>(() => PrescriptionRules.Verify(rx, 5, Now));

        rx.CopyRetained = true;
        PrescriptionRules.Verify(rx, 5, Now);
        Assert.Equal(PrescriptionStatus.Verified, rx.Status);
    }

    [Fact]
    public void ItemsToFill_EnteredPrescription_Throws()
    {
        Assert.Throws<PrescriptionRuleException>(() => PrescriptionRules.ItemsToFill(NewRx()));
    }

    [Fact]
    public void Fill_ThenRefills_UntilNoneLeft()
    {
        var rx = NewRx(refills: 1);
        PrescriptionRules.Verify(rx, 5, Now);

        PrescriptionRules.RecordFill(rx, PrescriptionRules.ItemsToFill(rx), 5, Now);
        Assert.Equal(PrescriptionStatus.Dispensed, rx.Status);
        Assert.Equal(0, rx.Items.Single().RefillsUsed);

        PrescriptionRules.RecordFill(rx, PrescriptionRules.ItemsToFill(rx), 5, Now);
        Assert.Equal(1, rx.Items.Single().RefillsUsed);
        Assert.Equal(20, rx.Items.Single().QuantityDispensed);

        Assert.Throws<PrescriptionRuleException>(() => PrescriptionRules.ItemsToFill(rx));
    }

    [Fact]
    public void Reject_DispensedPrescription_Throws()
    {
        var rx = NewRx();
        PrescriptionRules.Verify(rx, 5, Now);
        PrescriptionRules.RecordFill(rx, PrescriptionRules.ItemsToFill(rx), 5, Now);

        Assert.Throws<PrescriptionRuleException>(() => PrescriptionRules.Reject(rx, "Too late"));
    }

    [Fact]
    public void ValidateItemMedicine_RefusesNdpsAndInactive()
    {
        Assert.NotNull(PrescriptionRules.ValidateItemMedicine(new Medicine { Name = "N", Schedule = DrugSchedule.Ndps }));
        Assert.NotNull(PrescriptionRules.ValidateItemMedicine(new Medicine { Name = "I", Schedule = DrugSchedule.H, IsActive = false }));
        Assert.Null(PrescriptionRules.ValidateItemMedicine(new Medicine { Name = "OK", Schedule = DrugSchedule.H1 }));
    }
}
