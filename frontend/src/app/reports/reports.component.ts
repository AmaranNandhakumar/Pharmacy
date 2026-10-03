import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { localDate } from '../sales/sale.service';
import { DailySalesReport, ExpiringReport, GstSummaryReport, StockValuationReport } from './report.models';
import { ReportService, downloadCsv } from './report.service';

type Tab = 'daily' | 'gst' | 'stock' | 'expiring';

/** Owner's reports: takings, GST for filing, stock value and expiry risk. CSV export for the accountant. */
@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, DecimalPipe],
  template: `
    <div class="head">
      <h1>Reports</h1>
      <div class="no-print actions">
        <button class="btn-link" (click)="exportCsv()" [disabled]="!hasData()">Download CSV</button>
        <button class="btn" (click)="print()" [disabled]="!hasData()">Print</button>
      </div>
    </div>

    <div class="tabs no-print">
      <button [class.active]="tab === 'daily'" (click)="show('daily')">Daily sales</button>
      <button [class.active]="tab === 'gst'" (click)="show('gst')">GST summary</button>
      <button [class.active]="tab === 'stock'" (click)="show('stock')">Stock valuation</button>
      <button [class.active]="tab === 'expiring'" (click)="show('expiring')">Expiring stock</button>
    </div>

    <div class="card">
      @if (tab === 'daily' || tab === 'gst') {
        <div class="toolbar no-print">
          <label>From <input type="date" [(ngModel)]="from" (change)="load()" [max]="to"></label>
          <label>To <input type="date" [(ngModel)]="to" (change)="load()" [min]="from"></label>
          <button class="btn-link" (click)="thisMonth()">This month</button>
          <button class="btn-link" (click)="lastMonth()">Last month</button>
        </div>
      }
      @if (tab === 'expiring') {
        <div class="toolbar no-print">
          <label>Expiring within
            <select [(ngModel)]="withinDays" (change)="load()">
              <option [ngValue]="30">30 days</option>
              <option [ngValue]="60">60 days</option>
              <option [ngValue]="90">90 days</option>
              <option [ngValue]="180">6 months</option>
            </select>
          </label>
        </div>
      }
      @if (error) { <p class="error">{{ error }}</p> }

      @if (tab === 'daily' && daily; as r) {
        <h2>Daily sales, {{ r.from | date: 'd MMM' }} to {{ r.to | date: 'd MMM yyyy' }}</h2>
        <div class="tiles">
          <div><span class="muted">Takings</span><strong>{{ r.totals.total | currency: 'INR' }}</strong></div>
          <div><span class="muted">Bills</span><strong>{{ r.totals.bills }}</strong></div>
          <div><span class="muted">Cash / UPI / Card</span><strong class="small">{{ r.totals.cash | currency: 'INR':'symbol':'1.0-0' }} / {{ r.totals.upi | currency: 'INR':'symbol':'1.0-0' }} / {{ r.totals.card | currency: 'INR':'symbol':'1.0-0' }}</strong></div>
          <div><span class="muted">Voided bills</span><strong>{{ r.totals.voidedBills }}</strong></div>
        </div>
        <table>
          <thead><tr><th>Date</th><th class="num">Bills</th><th class="num">Discount</th><th class="num">Taxable</th><th class="num">GST</th>
                     <th class="num">Cash</th><th class="num">UPI</th><th class="num">Card</th><th class="num">Total</th></tr></thead>
          <tbody>
            @for (d of r.days; track d.date) {
              <tr [class.quiet]="d.bills === 0">
                <td>{{ d.date | date: 'EEE d MMM' }}</td>
                <td class="num">{{ d.bills }} @if (d.voidedBills) { <span class="muted small">(+{{ d.voidedBills }} void)</span> }</td>
                <td class="num">{{ d.discount | number: '1.2-2' }}</td>
                <td class="num">{{ d.taxableValue | number: '1.2-2' }}</td>
                <td class="num">{{ d.cgst + d.sgst | number: '1.2-2' }}</td>
                <td class="num">{{ d.cash | number: '1.2-2' }}</td>
                <td class="num">{{ d.upi | number: '1.2-2' }}</td>
                <td class="num">{{ d.card | number: '1.2-2' }}</td>
                <td class="num"><strong>{{ d.total | number: '1.2-2' }}</strong></td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr><td>Total</td><td class="num">{{ r.totals.bills }}</td><td class="num">{{ r.totals.discount | number: '1.2-2' }}</td>
                <td class="num">{{ r.totals.taxableValue | number: '1.2-2' }}</td><td class="num">{{ r.totals.cgst + r.totals.sgst | number: '1.2-2' }}</td>
                <td class="num">{{ r.totals.cash | number: '1.2-2' }}</td><td class="num">{{ r.totals.upi | number: '1.2-2' }}</td>
                <td class="num">{{ r.totals.card | number: '1.2-2' }}</td><td class="num">{{ r.totals.total | number: '1.2-2' }}</td></tr>
          </tfoot>
        </table>
      }

      @if (tab === 'gst' && gst; as r) {
        <h2>GST summary, {{ r.from | date: 'd MMM' }} to {{ r.to | date: 'd MMM yyyy' }}</h2>
        <p class="muted">
          {{ r.bills }} bills{{ r.voidedBills ? ', ' + r.voidedBills + ' voided' : '' }}
          @if (r.firstInvoiceNo) { · invoices {{ r.firstInvoiceNo }} to {{ r.lastInvoiceNo }} }
          · all B2C, intra-state (CGST + SGST)
        </p>
        <h3>By GST rate <span class="muted small">(GSTR-3B, table 3.1a)</span></h3>
        <table>
          <thead><tr><th>Rate</th><th class="num">Taxable value</th><th class="num">CGST</th><th class="num">SGST</th><th class="num">Total</th></tr></thead>
          <tbody>
            @for (g of r.byRate; track g.gstRatePercent) {
              <tr><td>{{ g.gstRatePercent }}%</td><td class="num">{{ g.taxableValue | number: '1.2-2' }}</td><td class="num">{{ g.cgst | number: '1.2-2' }}</td>
                  <td class="num">{{ g.sgst | number: '1.2-2' }}</td><td class="num">{{ g.total | number: '1.2-2' }}</td></tr>
            } @empty { <tr><td colspan="5" class="muted">No sales in this period.</td></tr> }
          </tbody>
          <tfoot>
            <tr><td>Total</td><td class="num">{{ r.totals.taxableValue | number: '1.2-2' }}</td><td class="num">{{ r.totals.cgst | number: '1.2-2' }}</td>
                <td class="num">{{ r.totals.sgst | number: '1.2-2' }}</td><td class="num">{{ r.totals.total | number: '1.2-2' }}</td></tr>
          </tfoot>
        </table>
        <h3>HSN-wise summary <span class="muted small">(GSTR-1, table 12)</span></h3>
        <table>
          <thead><tr><th>HSN</th><th>Rate</th><th class="num">Qty (units)</th><th class="num">Taxable value</th><th class="num">CGST</th><th class="num">SGST</th><th class="num">Total</th></tr></thead>
          <tbody>
            @for (h of r.byHsn; track h.hsnCode + '-' + h.gstRatePercent) {
              <tr><td>{{ h.hsnCode }}</td><td>{{ h.gstRatePercent }}%</td><td class="num">{{ h.quantity }}</td><td class="num">{{ h.taxableValue | number: '1.2-2' }}</td>
                  <td class="num">{{ h.cgst | number: '1.2-2' }}</td><td class="num">{{ h.sgst | number: '1.2-2' }}</td><td class="num">{{ h.total | number: '1.2-2' }}</td></tr>
            }
          </tbody>
        </table>
        <p class="muted small">A learning summary to hand to your accountant; check figures before filing.</p>
      }

      @if (tab === 'stock' && stock; as r) {
        <h2>Stock valuation as of {{ r.asOf | date: 'd MMM yyyy' }}</h2>
        <div class="tiles">
          <div><span class="muted">At purchase cost</span><strong>{{ r.purchaseValue | currency: 'INR' }}</strong></div>
          <div><span class="muted">At selling price</span><strong>{{ r.salesValue | currency: 'INR' }}</strong></div>
          <div><span class="muted">Units sellable</span><strong>{{ r.quantity | number }}</strong></div>
          <div><span class="muted">Expired, still on shelf</span><strong class="danger">{{ r.expiredPurchaseValue | currency: 'INR' }}</strong></div>
        </div>
        <table>
          <thead><tr><th>Medicine</th><th>Schedule</th><th class="num">Units</th><th class="num">Cost value</th><th class="num">Sales value</th><th class="num">Expired units</th><th class="num">Expired cost</th></tr></thead>
          <tbody>
            @for (s of r.rows; track s.medicineId) {
              <tr>
                <td><a [routerLink]="['/medicines', s.medicineId]">{{ s.name }} {{ s.strength }}</a></td>
                <td>{{ s.schedule }}</td>
                <td class="num">{{ s.quantity }}</td>
                <td class="num">{{ s.purchaseValue | number: '1.2-2' }}</td>
                <td class="num">{{ s.salesValue | number: '1.2-2' }}</td>
                <td class="num" [class.danger]="s.expiredQuantity > 0">{{ s.expiredQuantity || '' }}</td>
                <td class="num" [class.danger]="s.expiredQuantity > 0">{{ s.expiredQuantity ? (s.expiredPurchaseValue | number: '1.2-2') : '' }}</td>
              </tr>
            } @empty { <tr><td colspan="7" class="muted">No stock on hand.</td></tr> }
          </tbody>
        </table>
      }

      @if (tab === 'expiring' && expiring; as r) {
        <h2>Expired or expiring within {{ r.withinDays }} days</h2>
        <p>Stock at risk (purchase cost): <strong class="danger">{{ r.purchaseValueAtRisk | currency: 'INR' }}</strong>.
           <span class="muted">Return near-expiry stock to the supplier while they still accept it.</span></p>
        <table>
          <thead><tr><th>Medicine</th><th>Batch</th><th>Expiry</th><th class="num">Days left</th><th class="num">Units</th><th class="num">Cost value</th><th>Supplier</th></tr></thead>
          <tbody>
            @for (e of r.rows; track e.batchId) {
              <tr [class.expired]="e.daysLeft < 0">
                <td><a [routerLink]="['/medicines', e.medicineId]">{{ e.medicineName }}</a></td>
                <td>{{ e.batchNumber }}</td>
                <td>{{ e.expiryDate | date: 'MMM yyyy' }}</td>
                <td class="num">{{ e.daysLeft < 0 ? 'expired' : e.daysLeft }}</td>
                <td class="num">{{ e.quantity }}</td>
                <td class="num">{{ e.purchaseValue | number: '1.2-2' }}</td>
                <td class="muted">{{ e.supplierName }}</td>
              </tr>
            } @empty { <tr><td colspan="7" class="muted">Nothing expiring in this window.</td></tr> }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    .actions { display: flex; gap: 1rem; align-items: center; }
    .tabs { display: flex; gap: .5rem; margin-bottom: 1rem; flex-wrap: wrap; }
    .tabs button { border: 1px solid var(--border); background: var(--surface); border-radius: 999px; padding: .35rem 1rem; cursor: pointer; }
    .tabs button.active { background: var(--brand); border-color: var(--brand); color: #fff; }
    .toolbar { display: flex; gap: 1rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
    .toolbar input, .toolbar select { padding: .4rem .5rem; border: 1px solid var(--border); border-radius: 6px; margin-left: .3rem; }
    h2 { font-size: 1.15rem; }
    h3 { font-size: 1rem; margin: 1.25rem 0 .5rem; }
    .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 1rem; margin-bottom: 1rem; }
    .tiles div { display: flex; flex-direction: column; gap: .2rem; padding: .75rem; border: 1px solid var(--border); border-radius: 8px; }
    .tiles strong { font-size: 1.25rem; }
    .tiles strong.small { font-size: .95rem; }
    .num { text-align: right; white-space: nowrap; }
    tfoot td { font-weight: 700; border-top: 2px solid var(--text); }
    .quiet td { color: var(--muted); }
    .expired td { color: var(--danger); }
    .danger { color: var(--danger); }
    .small { font-size: .78rem; }
    td a { color: var(--brand); text-decoration: none; }
  `]
})
export class ReportsComponent implements OnInit {
  tab: Tab = 'daily';
  from = localDate().slice(0, 8) + '01';
  to = localDate();
  withinDays = 90;
  error = '';

  daily: DailySalesReport | null = null;
  gst: GstSummaryReport | null = null;
  stock: StockValuationReport | null = null;
  expiring: ExpiringReport | null = null;

  constructor(private service: ReportService, private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab') as Tab | null;
    if (tab && ['daily', 'gst', 'stock', 'expiring'].includes(tab)) this.tab = tab;
    this.load();
  }

  show(tab: Tab): void {
    this.tab = tab;
    this.router.navigate([], { queryParams: { tab }, replaceUrl: true });
    this.load();
  }

  thisMonth(): void {
    this.from = localDate().slice(0, 8) + '01';
    this.to = localDate();
    this.load();
  }

  lastMonth(): void {
    const now = new Date();
    const first = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const last = new Date(now.getFullYear(), now.getMonth(), 0);
    const fmt = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.from = fmt(first);
    this.to = fmt(last);
    this.load();
  }

  load(): void {
    this.error = '';
    const fail = (err: { error?: { message?: string } }) => this.error = err.error?.message || 'Could not load the report.';
    switch (this.tab) {
      case 'daily': this.service.dailySales(this.from, this.to).subscribe({ next: r => this.daily = r, error: fail }); break;
      case 'gst': this.service.gstSummary(this.from, this.to).subscribe({ next: r => this.gst = r, error: fail }); break;
      case 'stock': this.service.stockValuation().subscribe({ next: r => this.stock = r, error: fail }); break;
      case 'expiring': this.service.expiring(this.withinDays).subscribe({ next: r => this.expiring = r, error: fail }); break;
    }
  }

  hasData(): boolean {
    return !!{ daily: this.daily, gst: this.gst, stock: this.stock, expiring: this.expiring }[this.tab];
  }

  print(): void {
    window.print();
  }

  exportCsv(): void {
    if (this.tab === 'daily' && this.daily) {
      downloadCsv(`daily-sales-${this.daily.from}-to-${this.daily.to}.csv`,
        ['Date', 'Bills', 'Voided', 'Gross', 'Discount', 'Taxable value', 'CGST', 'SGST', 'Cash', 'UPI', 'Card', 'Total'],
        this.daily.days.map(d => [d.date, d.bills, d.voidedBills, d.gross, d.discount, d.taxableValue, d.cgst, d.sgst, d.cash, d.upi, d.card, d.total]));
    } else if (this.tab === 'gst' && this.gst) {
      downloadCsv(`gst-hsn-summary-${this.gst.from}-to-${this.gst.to}.csv`,
        ['HSN', 'GST rate %', 'Quantity', 'Taxable value', 'CGST', 'SGST', 'Total'],
        this.gst.byHsn.map(h => [h.hsnCode, h.gstRatePercent, h.quantity, h.taxableValue, h.cgst, h.sgst, h.total]));
    } else if (this.tab === 'stock' && this.stock) {
      downloadCsv(`stock-valuation-${this.stock.asOf}.csv`,
        ['Medicine', 'Strength', 'Schedule', 'Units', 'Cost value', 'Sales value', 'Expired units', 'Expired cost'],
        this.stock.rows.map(s => [s.name, s.strength, s.schedule, s.quantity, s.purchaseValue, s.salesValue, s.expiredQuantity, s.expiredPurchaseValue]));
    } else if (this.tab === 'expiring' && this.expiring) {
      downloadCsv(`expiring-${this.expiring.asOf}-${this.expiring.withinDays}d.csv`,
        ['Medicine', 'Batch', 'Expiry', 'Days left', 'Units', 'Cost value', 'Supplier'],
        this.expiring.rows.map(e => [e.medicineName, e.batchNumber, e.expiryDate, e.daysLeft, e.quantity, e.purchaseValue, e.supplierName]));
    }
  }
}
