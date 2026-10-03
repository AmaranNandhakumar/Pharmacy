using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;

namespace Pharmacy.Tests;

public class GstCalculatorTests
{
    [Fact]
    public void Calculate_WorksTaxOutBackwardsFromInclusivePrice()
    {
        // 2 strips at ₹105 including 5% GST: taxable 200, GST 10 split 5 + 5
        var line = GstCalculator.Calculate(105m, 2, 5m, 0m);

        Assert.Equal(210m, line.Gross);
        Assert.Equal(0m, line.Discount);
        Assert.Equal(200m, line.TaxableValue);
        Assert.Equal(5m, line.Cgst);
        Assert.Equal(5m, line.Sgst);
        Assert.Equal(210m, line.Total);
    }

    [Fact]
    public void Calculate_AppliesDiscountBeforeTax()
    {
        var line = GstCalculator.Calculate(118m, 1, 18m, 10m);

        Assert.Equal(11.80m, line.Discount);
        Assert.Equal(106.20m, line.Total);
        Assert.Equal(90m, line.TaxableValue);
        Assert.Equal(8.10m, line.Cgst);
        Assert.Equal(8.10m, line.Sgst);
    }

    [Theory]
    [InlineData(45.50, 3, 5, 0)]
    [InlineData(33.33, 7, 18, 7.5)]
    [InlineData(12.99, 1, 0, 20)]
    [InlineData(0.01, 1, 5, 0)]
    public void Calculate_PartsAlwaysAddUpToTheTotal(double price, int qty, double rate, double discount)
    {
        var line = GstCalculator.Calculate((decimal)price, qty, (decimal)rate, (decimal)discount);

        Assert.Equal(line.Total, line.TaxableValue + line.Cgst + line.Sgst);
        Assert.Equal(line.Gross, line.Total + line.Discount);
        Assert.True(Math.Abs(line.Cgst - line.Sgst) <= 0.01m);
    }

    [Fact]
    public void Calculate_NilRated_HasNoTax()
    {
        var line = GstCalculator.Calculate(50m, 2, 0m, 0m);

        Assert.Equal(100m, line.TaxableValue);
        Assert.Equal(0m, line.Cgst + line.Sgst);
    }

    [Theory]
    [InlineData(2026, 10, 3, "2026-27")]
    [InlineData(2027, 3, 31, "2026-27")]
    [InlineData(2027, 4, 1, "2027-28")]
    [InlineData(2099, 6, 1, "2099-00")]
    public void FinancialYear_RunsAprilToMarch(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, GstCalculator.FinancialYear(new DateOnly(y, m, d)));
    }

    [Fact]
    public void InvoiceNo_IsZeroPadded()
    {
        Assert.Equal("2026-27/000042", GstCalculator.InvoiceNo("2026-27", 42));
    }

    [Theory]
    [InlineData(DrugSchedule.Otc, true)]
    [InlineData(DrugSchedule.G, true)]
    [InlineData(DrugSchedule.H, false)]
    [InlineData(DrugSchedule.H1, false)]
    [InlineData(DrugSchedule.X, false)]
    [InlineData(DrugSchedule.Ndps, false)]
    public void OnlyOtcAndScheduleG_SellOverTheCounter(DrugSchedule schedule, bool expected)
    {
        Assert.Equal(expected, SaleRules.CanSellOverTheCounter(new Medicine { Schedule = schedule }));
    }
}
