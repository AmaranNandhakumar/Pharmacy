import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { PurchaseOrder, PurchaseOrderLine, ReceiveLineRequest, poStatusLabel } from './purchasing.models';
import { PurchasingService } from './purchasing.service';

interface ReceiveRow {
  line: PurchaseOrderLine;
  batchNumber: string;
  expiryMonth: string;
  mrp: number | null;
  sellingPrice: number | null;
  purchaseRate: number | null;
  quantity: number;
}

/** A purchase order: print it for the supplier, mark it ordered, and receive deliveries against it. */
@Component({
  selector: 'app-purchase-order-detail',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, CurrencyPipe],
  template: `
    <div class="no-print bar">
      <a routerLink="/purchase-orders" class="back">← Purchase orders</a>
      @if (po) { <button class="btn-link" (click)="print()">Print</button> }
    </div>

    @if (po; as p) {
      <div class="card doc">
        <div class="title">
          <div>
            <h1>Purchase order {{ p.poNumber }}</h1>
            <p class="muted">
              To <strong>{{ p.supplier.name }}</strong>
              {{ p.supplier.gstin ? '· GSTIN ' + p.supplier.gstin : '' }}
              {{ p.supplier.drugLicenceNo ? '· DL ' + p.supplier.drugLicenceNo : '' }}
              {{ p.supplier.phone ? '· ' + p.supplier.phone : '' }}
            </p>
            <p class="muted small">Created {{ p.createdAt | date: 'd MMM yyyy' }} by {{ p.createdByName }}
              @if (p.orderedAt) { · ordered {{ p.orderedAt | date: 'd MMM yyyy' }} }
              @if (p.completedAt) { · completed {{ p.completedAt | date: 'd MMM yyyy' }} }
            </p>
            @if (p.notes) { <p>{{ p.notes }}</p> }
          </div>
          <span class="badge status">{{ label(p.status) }}</span>
        </div>

        <table>
          <thead><tr><th>Medicine</th><th>Pack</th><th class="num">Ordered</th><th class="num">Received</th><th class="num">Due</th><th class="num">Rate</th><th class="num no-print">In stock</th></tr></thead>
          <tbody>
            @for (l of p.lines; track l.id) {
              <tr>
                <td>{{ l.medicineName }} {{ l.strength }} <span class="muted small">{{ l.form }}</span></td>
                <td class="muted">{{ l.packSize }}</td>
                <td class="num">{{ l.quantityOrdered }}</td>
                <td class="num">{{ l.quantityReceived }}</td>
                <td class="num" [class.due]="l.quantityOutstanding > 0">{{ l.quantityOutstanding }}</td>
                <td class="num">{{ l.expectedRate === null ? '—' : (l.expectedRate | currency: 'INR') }}</td>
                <td class="num no-print">{{ l.sellableQuantity }}</td>
              </tr>
            }
          </tbody>
        </table>
        @if (p.estimatedValue !== null) { <p class="total">Estimated value: <strong>{{ p.estimatedValue | currency: 'INR' }}</strong></p> }
      </div>

      <div class="no-print">
        @if (canBuy) {
          <div class="actions">
            @if (p.status === 'Draft') {
              <a class="btn-link" [routerLink]="['/purchase-orders', p.id, 'edit']">Edit draft</a>
              <button class="btn" (click)="run(service.markOrdered(p.id))" [disabled]="busy">Mark as ordered</button>
            }
            @if (p.status !== 'Received' && p.status !== 'Closed' && p.status !== 'Cancelled') {
              <input class="reason" placeholder="Reason (optional)" [(ngModel)]="closeReason">
              <button class="btn-link danger" (click)="close()" [disabled]="busy">
                {{ p.unitsReceived > 0 ? 'Close short (stop waiting for the rest)' : 'Cancel order' }}
              </button>
            }
          </div>
        }

        @if (p.status === 'Ordered' || p.status === 'PartiallyReceived') {
          <div class="card">
            <h2>Receive a delivery</h2>
            <label class="field invoice">Supplier invoice no. * <input [(ngModel)]="invoiceNo" placeholder="As printed on their bill"></label>
            <table class="receive">
              <thead><tr><th>Medicine</th><th>Due</th><th>Batch no.</th><th>Expiry</th><th>MRP ₹</th><th>Selling ₹</th><th>Purchase ₹</th><th>Units</th><th></th></tr></thead>
              <tbody>
                @for (r of rows; track r; let i = $index) {
                  <tr>
                    <td>{{ r.line.medicineName }}</td>
                    <td>{{ r.line.quantityOutstanding }}</td>
                    <td><input [(ngModel)]="r.batchNumber" class="w-batch"></td>
                    <td><input type="month" [(ngModel)]="r.expiryMonth"></td>
                    <td><input type="number" step="0.01" min="0" [(ngModel)]="r.mrp" class="w-num"></td>
                    <td><input type="number" step="0.01" min="0" [(ngModel)]="r.sellingPrice" class="w-num"></td>
                    <td><input type="number" step="0.01" min="0" [(ngModel)]="r.purchaseRate" class="w-num"></td>
                    <td><input type="number" min="0" [max]="r.line.quantityOutstanding" [(ngModel)]="r.quantity" class="w-num"></td>
                    <td class="nowrap">
                      <button class="btn-link" (click)="splitBatch(i)" title="The same medicine arrived in two batches">+ batch</button>
                      @if (isSplit(i)) { <button class="btn-link danger" (click)="rows.splice(i, 1)">✕</button> }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <p class="muted small">Leave units at 0 for anything that didn't come. Selling price can't be above MRP.</p>
            @if (receiveProblem()) { <p class="error">{{ receiveProblem() }}</p> }
            @if (error) { <p class="error">{{ error }}</p> }
            <button class="btn" (click)="receive()" [disabled]="busy || !!receiveProblem()">Add delivery to stock</button>
          </div>
        } @else if (error) {
          <p class="error">{{ error }}</p>
        }
      </div>
    } @else if (loadError) {
      <p class="error">{{ loadError }}</p>
    }
  `,
  styles: [`
    .bar { display: flex; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .back { color: var(--brand); text-decoration: none; }
    .card { margin-bottom: 1.25rem; }
    .title { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    h1 { margin-bottom: .25rem; }
    .status { font-size: .9rem; padding: .25rem .8rem; }
    .num { text-align: right; }
    .due { font-weight: 600; color: #b45309; }
    .total { text-align: right; }
    .small { font-size: .8rem; }
    .actions { display: flex; gap: 1rem; align-items: center; margin-bottom: 1.25rem; flex-wrap: wrap; }
    .reason { padding: .45rem .6rem; border: 1px solid var(--border); border-radius: 6px; min-width: 220px; margin-left: auto; }
    .danger { color: var(--danger); }
    .invoice { max-width: 320px; }
    .receive td { padding: .35rem .25rem; }
    .receive input { padding: .3rem .4rem; border: 1px solid var(--border); border-radius: 6px; }
    .w-batch { width: 7rem; }
    .w-num { width: 5.5rem; }
    .nowrap { white-space: nowrap; }
    a.btn-link { text-decoration: none; }
  `]
})
export class PurchaseOrderDetailComponent implements OnInit {
  po: PurchaseOrder | null = null;
  rows: ReceiveRow[] = [];
  invoiceNo = '';
  closeReason = '';
  canBuy = this.auth.hasRole('Admin', 'Pharmacist');
  busy = false;
  error = '';
  loadError = '';
  label = poStatusLabel;

  constructor(private route: ActivatedRoute, public service: PurchasingService, private auth: AuthService) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.order(id).subscribe({
      next: po => this.show(po),
      error: err => this.loadError = err.status === 404 ? 'Purchase order not found.' : 'Could not load the purchase order.'
    });
  }

  print(): void {
    window.print();
  }

  splitBatch(i: number): void {
    const r = this.rows[i];
    this.rows.splice(i + 1, 0, { ...r, batchNumber: '', quantity: 0 });
  }

  isSplit(i: number): boolean {
    return i > 0 && this.rows[i - 1].line.id === this.rows[i].line.id;
  }

  /** The first thing wrong with the delivery form, or '' when it can be sent. */
  receiveProblem(): string {
    const filled = this.rows.filter(r => r.quantity > 0);
    if (!this.invoiceNo.trim()) return '';
    if (filled.length === 0) return 'Enter the units received for at least one line.';
    for (const r of filled) {
      if (!r.batchNumber.trim() || !r.expiryMonth || !r.mrp || !r.sellingPrice || r.purchaseRate === null)
        return `Fill in batch, expiry and prices for ${r.line.medicineName}.`;
      if (r.sellingPrice > r.mrp) return `Selling price of ${r.line.medicineName} is above its MRP.`;
    }
    for (const line of this.po?.lines ?? []) {
      const total = filled.filter(r => r.line.id === line.id).reduce((s, r) => s + r.quantity, 0);
      if (total > line.quantityOutstanding) return `Only ${line.quantityOutstanding} more units of ${line.medicineName} were ordered.`;
    }
    return '';
  }

  receive(): void {
    if (!this.invoiceNo.trim()) { this.error = 'Enter the supplier invoice number.'; return; }
    const lines: ReceiveLineRequest[] = this.rows.filter(r => r.quantity > 0).map(r => ({
      lineId: r.line.id,
      batchNumber: r.batchNumber.trim(),
      expiryDate: lastDayOfMonth(r.expiryMonth),
      mrp: r.mrp!,
      sellingPrice: r.sellingPrice!,
      purchaseRate: r.purchaseRate!,
      quantity: r.quantity
    }));
    this.run(this.service.receive(this.po!.id, this.invoiceNo.trim(), lines), () => this.invoiceNo = '');
  }

  close(): void {
    this.run(this.service.close(this.po!.id, this.closeReason.trim() || null));
  }

  run(call: Observable<PurchaseOrder>, after?: () => void): void {
    this.busy = true;
    this.error = '';
    call.subscribe({
      next: po => { this.busy = false; this.show(po); after?.(); },
      error: err => { this.busy = false; this.error = err.error?.message || 'Something went wrong. Try again.'; }
    });
  }

  private show(po: PurchaseOrder): void {
    this.po = po;
    this.rows = po.lines.filter(l => l.quantityOutstanding > 0).map(l => ({
      line: l, batchNumber: '', expiryMonth: '', mrp: null, sellingPrice: null, purchaseRate: l.expectedRate, quantity: l.quantityOutstanding
    }));
  }
}

/** Indian packs print expiry as month/year; stock is good until the end of that month (same as M1 receiving). */
function lastDayOfMonth(month: string): string {
  const [y, m] = month.split('-').map(Number);
  const d = new Date(y, m, 0);
  return `${y}-${String(m).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
