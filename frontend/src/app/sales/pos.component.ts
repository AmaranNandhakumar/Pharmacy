import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, of, switchMap } from 'rxjs';
import { Medicine, MedicineDetail } from '../medicines/medicine.models';
import { MedicineService } from '../medicines/medicine.service';
import { BillableFill, MAX_DISCOUNT_PERCENT, PAYMENT_METHODS, PaymentMethod } from './sale.models';
import { SaleService } from './sale.service';

interface CartLine {
  medicine: Medicine;
  quantity: number;
  /** Price of the batch that will be picked first; the invoice has the exact figures. */
  estimatedPrice: number;
}

/** The billing counter: shelf (OTC / Schedule G) items plus dispensed prescriptions, on one GST invoice. */
@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <h1>Counter</h1>
    <div class="layout">
      <div>
        <div class="card">
          <h2>Shelf items</h2>
          <input class="search" placeholder="Search OTC medicine by name or barcode" [(ngModel)]="query" (ngModelChange)="search$.next(query)" autofocus>
          <ul class="results">
            @for (m of results; track m.id) {
              <li (click)="add(m)" [class.disabled]="m.sellableQuantity === 0">
                <strong>{{ m.name }}</strong> {{ m.strength }} <span class="muted">{{ m.form }}</span>
                <span class="muted small">{{ m.sellableQuantity }} in stock</span>
              </li>
            }
          </ul>
          @if (rxHint) { <p class="muted small">{{ rxHint }}</p> }
        </div>

        <div class="card">
          <h2>Dispensed prescriptions to bill</h2>
          @for (f of fills; track f.fillId) {
            <label class="fill" [class.other]="selectedPatientId() !== null && selectedPatientId() !== f.patientId">
              <input type="checkbox" [checked]="selectedFills.has(f.fillId)" (change)="toggleFill(f)"
                     [disabled]="selectedPatientId() !== null && selectedPatientId() !== f.patientId">
              <div>
                <strong>{{ f.patientName }}</strong> · Rx #{{ f.prescriptionId }} {{ f.isRefill ? '(refill)' : '' }}
                <span class="muted small">dispensed {{ f.dispensedAt | date: 'd MMM, h:mm a' }}</span>
                <div class="muted small">
                  @for (l of f.lines; track $index) { {{ l.medicineName }} × {{ l.quantity }}{{ $last ? '' : ', ' }} }
                </div>
              </div>
              <span>{{ f.total | currency: 'INR' }}</span>
            </label>
          } @empty {
            <p class="muted">Nothing waiting. Prescriptions appear here after a pharmacist dispenses them.</p>
          }
        </div>
      </div>

      <div class="card cart">
        <h2>Bill</h2>
        <table>
          <tbody>
            @for (f of chosenFills(); track f.fillId) {
              <tr><td colspan="2">Rx #{{ f.prescriptionId }} · {{ f.lines.length }} item(s)</td><td class="num">{{ f.total | currency: 'INR' }}</td></tr>
            }
            @for (line of cart; track line.medicine.id; let i = $index) {
              <tr>
                <td>{{ line.medicine.name }} {{ line.medicine.strength }}</td>
                <td><input type="number" min="1" [max]="line.medicine.sellableQuantity" [(ngModel)]="line.quantity" class="qty"></td>
                <td class="num">{{ line.estimatedPrice * line.quantity | currency: 'INR' }}
                  <button class="btn-link danger" (click)="cart.splice(i, 1)">✕</button></td>
              </tr>
            }
            @if (cart.length === 0 && selectedFills.size === 0) {
              <tr><td colspan="3" class="muted">Add shelf items or pick a prescription.</td></tr>
            }
          </tbody>
        </table>

        <label class="field">Customer name (optional) <input [(ngModel)]="customerName" [disabled]="selectedFills.size > 0"
               [placeholder]="selectedFills.size > 0 ? 'Billed to the prescription patient' : 'Walk-in'"></label>
        <label class="field">Discount %
          <input type="number" min="0" [max]="maxDiscount" [(ngModel)]="discountPercent">
        </label>
        <div class="pay">
          @for (p of paymentMethods; track p.value) {
            <label><input type="radio" name="pay" [value]="p.value" [(ngModel)]="paymentMethod"> {{ p.label }}</label>
          }
        </div>

        <div class="total">
          <span>Estimated total</span>
          <strong>{{ estimatedTotal() | currency: 'INR' }}</strong>
        </div>
        <p class="muted small">Prices include GST. The invoice shows the exact batch, CGST and SGST.</p>
        @if (error) { <p class="error">{{ error }}</p> }
        <button class="btn wide" (click)="checkout()" [disabled]="saving || (cart.length === 0 && selectedFills.size === 0) || !validDiscount()">
          Take payment and print bill
        </button>
      </div>
    </div>
  `,
  styles: [`
    .layout { display: grid; grid-template-columns: 1fr 380px; gap: 1.25rem; align-items: start; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
    .card { margin-bottom: 1.25rem; }
    .search { width: 100%; padding: .55rem .7rem; border: 1px solid var(--border); border-radius: 6px; font-size: .95rem; }
    .results { list-style: none; padding: 0; margin: .25rem 0 0; }
    .results li { padding: .45rem .5rem; cursor: pointer; border-bottom: 1px solid var(--border); display: flex; gap: .5rem; align-items: center; }
    .results li:hover { background: #f0fdfa; }
    .results li.disabled { opacity: .5; pointer-events: none; }
    .fill { display: flex; gap: .75rem; align-items: flex-start; padding: .6rem 0; border-bottom: 1px solid var(--border); cursor: pointer; }
    .fill > div { flex: 1; }
    .fill.other { opacity: .45; }
    .small { font-size: .8rem; }
    .cart td { padding: .4rem .25rem; }
    .num { text-align: right; white-space: nowrap; }
    .qty { width: 4rem; padding: .3rem; border: 1px solid var(--border); border-radius: 6px; }
    .pay { display: flex; gap: 1rem; margin-bottom: 1rem; }
    .total { display: flex; justify-content: space-between; font-size: 1.3rem; padding: .75rem 0; border-top: 2px solid var(--text); }
    .wide { width: 100%; padding: .8rem; font-size: 1rem; }
    .danger { color: var(--danger); margin-left: .25rem; }
  `]
})
export class PosComponent implements OnInit {
  query = '';
  results: Medicine[] = [];
  rxHint = '';
  search$ = new Subject<string>();

  cart: CartLine[] = [];
  fills: BillableFill[] = [];
  selectedFills = new Set<number>();

  customerName = '';
  discountPercent = 0;
  maxDiscount = MAX_DISCOUNT_PERCENT;
  paymentMethod: PaymentMethod = 'Cash';
  paymentMethods = PAYMENT_METHODS;
  saving = false;
  error = '';

  constructor(
    private medicines: MedicineService,
    private sales: SaleService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(250),
      switchMap(q => q.trim().length < 2 ? of([]) : this.medicines.search(q))
    ).subscribe(list => {
      this.results = list.filter(m => m.schedule === 'Otc' || m.schedule === 'G');
      const hidden = list.length - this.results.length;
      this.rxHint = hidden > 0 ? `${hidden} prescription medicine(s) hidden: those are billed from a dispensed prescription.` : '';
    });

    const fillId = Number(this.route.snapshot.queryParamMap.get('fillId'));
    this.sales.billableFills().subscribe(fills => {
      this.fills = fills;
      if (fillId && fills.some(f => f.fillId === fillId)) this.selectedFills.add(fillId);
    });
  }

  add(m: Medicine): void {
    this.query = '';
    this.results = [];
    const existing = this.cart.find(l => l.medicine.id === m.id);
    if (existing) { existing.quantity++; return; }

    this.medicines.get(m.id).subscribe((detail: MedicineDetail) => {
      const next = detail.batches
        .filter(b => !b.isExpired && b.quantityOnHand > 0)
        .sort((a, b) => a.expiryDate.localeCompare(b.expiryDate))[0];
      this.cart.push({ medicine: m, quantity: 1, estimatedPrice: next?.sellingPrice ?? 0 });
    });
  }

  toggleFill(f: BillableFill): void {
    if (this.selectedFills.has(f.fillId)) this.selectedFills.delete(f.fillId);
    else this.selectedFills.add(f.fillId);
  }

  chosenFills(): BillableFill[] {
    return this.fills.filter(f => this.selectedFills.has(f.fillId));
  }

  /** A bill holds one patient's prescriptions; others are greyed out once one is picked. */
  selectedPatientId(): number | null {
    return this.chosenFills()[0]?.patientId ?? null;
  }

  validDiscount(): boolean {
    return this.discountPercent >= 0 && this.discountPercent <= this.maxDiscount;
  }

  estimatedTotal(): number {
    const gross = this.chosenFills().reduce((s, f) => s + f.total, 0)
      + this.cart.reduce((s, l) => s + l.estimatedPrice * l.quantity, 0);
    return gross * (1 - (this.discountPercent || 0) / 100);
  }

  checkout(): void {
    this.saving = true;
    this.error = '';
    this.sales.create({
      patientId: this.selectedPatientId(),
      customerName: this.selectedFills.size > 0 ? null : (this.customerName.trim() || null),
      paymentMethod: this.paymentMethod,
      discountPercent: this.discountPercent || 0,
      items: this.cart.map(l => ({ medicineId: l.medicine.id, quantity: l.quantity })),
      prescriptionFillIds: [...this.selectedFills]
    }).subscribe({
      next: sale => this.router.navigate(['/sales', sale.id], { queryParams: { print: 1 } }),
      error: err => {
        this.saving = false;
        this.error = err.error?.message || 'Could not complete the sale.';
      }
    });
  }
}
