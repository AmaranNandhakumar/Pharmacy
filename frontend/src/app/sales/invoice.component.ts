import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { Sale } from './sale.models';
import { SaleService } from './sale.service';

/** A GST tax invoice in the layout Indian retail pharmacies print (A5). */
@Component({
  selector: 'app-invoice',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, CurrencyPipe, DecimalPipe],
  template: `
    <div class="no-print bar">
      <a routerLink="/sales" class="back">← Sales</a>
      <a routerLink="/pos" class="btn-link">New bill</a>
      <button class="btn" (click)="print()" [disabled]="!sale">Print</button>
    </div>

    @if (sale; as s) {
      <div class="invoice card" [class.voided]="s.status === 'Voided'">
        <header>
          <div>
            <h1>{{ s.pharmacy.name }}</h1>
            <div>{{ s.pharmacy.address }} {{ s.pharmacy.phone ? '· Ph ' + s.pharmacy.phone : '' }}</div>
            <div class="small">GSTIN {{ s.pharmacy.gstin }} · DL No. {{ s.pharmacy.drugLicence20 }}, {{ s.pharmacy.drugLicence21 }}</div>
          </div>
          <div class="right">
            <strong>TAX INVOICE</strong>
            <div>No. {{ s.invoiceNo }}</div>
            <div>{{ s.createdAt | date: 'd MMM yyyy, h:mm a' }}</div>
          </div>
        </header>

        <div class="party">
          <div><span class="muted">Bill to:</span> {{ s.customerName || 'Cash customer' }} {{ s.patientAddress ? '· ' + s.patientAddress : '' }}</div>
          <div><span class="muted">Payment:</span> {{ s.paymentMethod === 'Upi' ? 'UPI' : s.paymentMethod }}</div>
        </div>

        <table>
          <thead>
            <tr><th>#</th><th>Item</th><th>HSN</th><th>Batch</th><th>Exp</th><th class="num">MRP</th><th class="num">Qty</th>
                <th class="num">Rate</th><th class="num">GST%</th><th class="num">Amount</th></tr>
          </thead>
          <tbody>
            @for (i of s.items; track i.id; let n = $index) {
              <tr>
                <td>{{ n + 1 }}</td>
                <td>{{ i.medicineName }} @if (i.prescriptionItemId) { <span class="rx">Rx</span> }</td>
                <td>{{ i.hsnCode }}</td>
                <td>{{ i.batchNumber }}</td>
                <td>{{ i.expiryDate | date: 'MM/yy' }}</td>
                <td class="num">{{ i.mrp | number: '1.2-2' }}</td>
                <td class="num">{{ i.quantity }}</td>
                <td class="num">{{ i.unitPrice | number: '1.2-2' }}</td>
                <td class="num">{{ i.gstRatePercent }}</td>
                <td class="num">{{ i.grossAmount | number: '1.2-2' }}</td>
              </tr>
            }
          </tbody>
        </table>

        <div class="foot">
          <table class="gst">
            <thead><tr><th>GST %</th><th class="num">Taxable</th><th class="num">CGST</th><th class="num">SGST</th></tr></thead>
            <tbody>
              @for (g of s.gstSummary; track g.gstRatePercent) {
                <tr>
                  <td>{{ g.gstRatePercent }}%</td>
                  <td class="num">{{ g.taxableValue | number: '1.2-2' }}</td>
                  <td class="num">{{ g.cgst | number: '1.2-2' }}</td>
                  <td class="num">{{ g.sgst | number: '1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div><span>Gross</span><span>{{ s.grossAmount | currency: 'INR' }}</span></div>
            @if (s.discount > 0) { <div><span>Discount ({{ s.discountPercent }}%)</span><span>− {{ s.discount | currency: 'INR' }}</span></div> }
            <div class="muted"><span>Taxable value</span><span>{{ s.taxableValue | currency: 'INR' }}</span></div>
            <div class="muted"><span>CGST + SGST</span><span>{{ s.cgst + s.sgst | currency: 'INR' }}</span></div>
            <div class="grand"><span>Total (incl. GST)</span><span>{{ s.total | currency: 'INR' }}</span></div>
          </div>
        </div>

        <footer class="small">
          <div>Pharmacist: {{ s.pharmacy.registeredPharmacistName }} (Reg. {{ s.pharmacy.registeredPharmacistRegNo }}) · Billed by {{ s.billedByName }}</div>
          <div class="muted">Prices are inclusive of GST. Medicines once sold are taken back only as per store policy.</div>
        </footer>
        @if (s.status === 'Voided') {
          <div class="void-stamp">VOIDED</div>
        }
      </div>

      @if (s.status === 'Voided') {
        <p class="error no-print">Voided by {{ s.voidedByName }} on {{ s.voidedAt | date: 'd MMM, h:mm a' }}: {{ s.voidReason }}</p>
      } @else if (isAdmin) {
        <div class="no-print void">
          <input placeholder="Reason for voiding (stock goes back on the shelf)" [(ngModel)]="voidReason">
          <button class="btn-link danger" (click)="voidSale()" [disabled]="busy || voidReason.trim().length < 3">Void this sale</button>
        </div>
      }
      @if (error) { <p class="error no-print">{{ error }}</p> }
    } @else if (loadError) {
      <p class="error">{{ loadError }}</p>
    }
  `,
  styles: [`
    .bar { display: flex; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .bar .btn { margin-left: auto; }
    .back { color: var(--brand); text-decoration: none; }
    .invoice { position: relative; max-width: 820px; font-size: .88rem; }
    header { display: flex; justify-content: space-between; gap: 1rem; border-bottom: 2px solid var(--text); padding-bottom: .6rem; }
    header h1 { margin: 0 0 .2rem; font-size: 1.3rem; }
    .right { text-align: right; }
    .party { display: flex; justify-content: space-between; padding: .6rem 0; border-bottom: 1px solid var(--border); }
    th, td { padding: .3rem .35rem; font-size: .82rem; }
    .num { text-align: right; }
    .rx { font-size: .7rem; color: var(--danger); border: 1px solid var(--danger); border-radius: 3px; padding: 0 .2rem; }
    .foot { display: flex; justify-content: space-between; gap: 1.5rem; margin-top: .75rem; align-items: flex-start; }
    .gst { width: auto; }
    .totals { min-width: 260px; }
    .totals div { display: flex; justify-content: space-between; padding: .15rem 0; }
    .grand { font-size: 1.15rem; font-weight: 700; border-top: 2px solid var(--text); margin-top: .3rem; padding-top: .4rem !important; }
    footer { margin-top: 1rem; border-top: 1px solid var(--border); padding-top: .5rem; }
    .small { font-size: .78rem; }
    .void-stamp { position: absolute; top: 40%; left: 25%; font-size: 4rem; color: rgba(185, 28, 28, .25); transform: rotate(-20deg); font-weight: 800; }
    .void { display: flex; gap: .75rem; margin-top: 1rem; }
    .void input { flex: 1; max-width: 420px; padding: .45rem .6rem; border: 1px solid var(--border); border-radius: 6px; }
    .danger { color: var(--danger); }
  `]
})
export class InvoiceComponent implements OnInit {
  sale: Sale | null = null;
  isAdmin = this.auth.hasRole('Admin');
  voidReason = '';
  busy = false;
  error = '';
  loadError = '';

  constructor(private route: ActivatedRoute, private sales: SaleService, private auth: AuthService) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    const printNow = this.route.snapshot.queryParamMap.get('print') === '1';
    this.sales.get(id).subscribe({
      next: s => {
        this.sale = s;
        // Straight from checkout: open the print dialog once the invoice has rendered
        if (printNow) setTimeout(() => window.print(), 300);
      },
      error: err => this.loadError = err.status === 404 ? 'Sale not found.' : 'Could not load the sale.'
    });
  }

  print(): void {
    window.print();
  }

  voidSale(): void {
    this.busy = true;
    this.error = '';
    this.sales.void(this.sale!.id, this.voidReason.trim()).subscribe({
      next: s => { this.busy = false; this.sale = s; },
      error: err => { this.busy = false; this.error = err.error?.message || 'Could not void the sale.'; }
    });
  }
}
