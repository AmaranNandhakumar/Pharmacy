import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { Batch, MedicineDetail, scheduleLabel } from './medicine.models';
import { MedicineFormComponent } from './medicine-form.component';
import { MedicineService } from './medicine.service';

@Component({
  selector: 'app-medicine-detail',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DatePipe, CurrencyPipe, MedicineFormComponent],
  template: `
    <a routerLink="/medicines" class="back">← Medicines</a>

    @if (medicine; as m) {
      @if (editing) {
        <app-medicine-form [medicine]="m" (saved)="editing = false; load()" (cancelled)="editing = false"></app-medicine-form>
      } @else {
        <div class="card info">
          <div class="title">
            <div>
              <h1>{{ m.name }} {{ m.strength }}</h1>
              <p class="muted">{{ m.genericName }} · {{ m.form }} {{ m.packSize ? '· ' + m.packSize : '' }} {{ m.manufacturer ? '· ' + m.manufacturer : '' }}</p>
            </div>
            @if (canEdit && m.isActive) {
              <div class="actions">
                <button class="btn" (click)="editing = true">Edit</button>
                <button class="btn-link danger" (click)="deactivate()">Deactivate</button>
              </div>
            }
          </div>
          <div class="facts">
            <span><span class="badge" [class.rx]="m.requiresPrescription">{{ label(m.schedule) }}</span></span>
            <span>HSN {{ m.hsnCode }}</span>
            <span>GST {{ m.gstRatePercent }}%</span>
            <span>Reorder at {{ m.reorderLevel }}</span>
            <span [class.low]="m.sellableQuantity <= m.reorderLevel">{{ m.sellableQuantity }} sellable</span>
            @if (!m.isActive) { <span class="error">Inactive</span> }
          </div>
        </div>
      }

      <div class="card">
        <div class="title">
          <h2>Batches</h2>
          @if (m.isActive) { <button class="btn" (click)="receiving = !receiving">{{ receiving ? 'Close' : 'Receive stock' }}</button> }
        </div>

        @if (receiving) {
          <form class="receive" [formGroup]="receiveForm" (ngSubmit)="receive()">
            <div class="grid">
              <label class="field">Batch no. * <input formControlName="batchNumber"></label>
              <label class="field">Expiry * <input type="month" formControlName="expiryMonth"></label>
              <label class="field">Quantity (units) * <input type="number" min="1" formControlName="quantity"></label>
              <label class="field">MRP per unit (₹) * <input type="number" step="0.01" min="0" formControlName="mrp"></label>
              <label class="field">Selling price (₹) * <input type="number" step="0.01" min="0" formControlName="sellingPrice"></label>
              <label class="field">Purchase rate (₹) <input type="number" step="0.01" min="0" formControlName="purchaseRate"></label>
              <label class="field">Supplier <input formControlName="supplierName"></label>
              <label class="field">Supplier invoice no. <input formControlName="supplierInvoiceNo"></label>
            </div>
            @if (sellingAboveMrp()) { <p class="error">Selling price can't be above the MRP.</p> }
            @if (receiveError) { <p class="error">{{ receiveError }}</p> }
            <button class="btn" type="submit" [disabled]="receiveForm.invalid || sellingAboveMrp() || saving">Add to stock</button>
          </form>
        }

        <table>
          <thead><tr><th>Batch</th><th>Expiry</th><th>MRP</th><th>Selling</th><th>On hand</th><th>Supplier</th><th></th></tr></thead>
          <tbody>
            @for (b of m.batches; track b.id) {
              <tr [class.expired]="b.isExpired" [class.empty]="b.quantityOnHand === 0">
                <td>{{ b.batchNumber }}</td>
                <td>{{ b.expiryDate | date: 'MMM yyyy' }} @if (b.isExpired) { <span class="error small">expired</span> }</td>
                <td>{{ b.mrp | currency: 'INR' }}</td>
                <td>{{ b.sellingPrice | currency: 'INR' }}</td>
                <td>{{ b.quantityOnHand }}</td>
                <td class="muted">{{ b.supplierName }} {{ b.supplierInvoiceNo ? '(' + b.supplierInvoiceNo + ')' : '' }}</td>
                <td>@if (canAdjust) { <button class="btn-link" (click)="startAdjust(b)">Adjust</button> }</td>
              </tr>
              @if (adjusting?.id === b.id) {
                <tr class="adjust-row">
                  <td colspan="7">
                    <form class="adjust" [formGroup]="adjustForm" (ngSubmit)="adjust()">
                      <label class="field">Change (− to remove) <input type="number" formControlName="quantityChange"></label>
                      <label class="field reason">Reason * <input formControlName="reason" placeholder="e.g. Damaged strip, count correction"></label>
                      <button class="btn" type="submit" [disabled]="adjustForm.invalid || adjustForm.value.quantityChange === 0 || saving">Save</button>
                      <button class="btn-link" type="button" (click)="adjusting = null">Cancel</button>
                    </form>
                    @if (adjustError) { <p class="error">{{ adjustError }}</p> }
                  </td>
                </tr>
              }
            } @empty {
              <tr><td colspan="7" class="muted">No stock received yet.</td></tr>
            }
          </tbody>
        </table>
      </div>
    } @else if (loadError) {
      <p class="error">{{ loadError }}</p>
    }
  `,
  styles: [`
    .back { display: inline-block; margin-bottom: 1rem; color: var(--brand); text-decoration: none; }
    .card { margin-bottom: 1.25rem; }
    .title { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    h1 { margin-bottom: .25rem; }
    .actions { display: flex; gap: 1rem; align-items: center; }
    .danger { color: var(--danger); }
    .facts { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-top: .75rem; font-size: .9rem; }
    .low { color: var(--danger); font-weight: 600; }
    .receive { border: 1px solid var(--border); border-radius: 8px; padding: 1rem; margin-bottom: 1rem; background: #f9fafb; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 0 1rem; }
    tr.expired td { color: var(--danger); }
    tr.empty td { color: var(--muted); }
    .small { font-size: .75rem; }
    .adjust { display: flex; gap: 1rem; align-items: flex-end; flex-wrap: wrap; }
    .adjust .field { margin-bottom: 0; }
    .adjust .reason { flex: 1; min-width: 220px; }
    app-medicine-form { display: block; margin-bottom: 1.25rem; }
  `]
})
export class MedicineDetailComponent implements OnInit {
  medicine: MedicineDetail | null = null;
  loadError = '';
  editing = false;
  receiving = false;
  adjusting: Batch | null = null;
  saving = false;
  receiveError = '';
  adjustError = '';
  canEdit = this.auth.hasRole('Admin', 'Pharmacist');
  canAdjust = this.auth.hasRole('Admin', 'Pharmacist');
  label = scheduleLabel;

  receiveForm = this.fb.nonNullable.group({
    batchNumber: ['', [Validators.required, Validators.maxLength(50)]],
    expiryMonth: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(1)]],
    mrp: [0, [Validators.required, Validators.min(0.01)]],
    sellingPrice: [0, [Validators.required, Validators.min(0.01)]],
    purchaseRate: [0, [Validators.min(0)]],
    supplierName: [''],
    supplierInvoiceNo: ['']
  });

  adjustForm = this.fb.nonNullable.group({
    quantityChange: [0, Validators.required],
    reason: ['', [Validators.required, Validators.minLength(3)]]
  });

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private service: MedicineService,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.get(id).subscribe({
      next: m => this.medicine = m,
      error: () => this.loadError = 'Medicine not found.'
    });
  }

  sellingAboveMrp(): boolean {
    const v = this.receiveForm.getRawValue();
    return v.sellingPrice > v.mrp;
  }

  receive(): void {
    if (!this.medicine || this.receiveForm.invalid) return;
    const v = this.receiveForm.getRawValue();
    this.saving = true;
    this.receiveError = '';

    this.service.receive({
      medicineId: this.medicine.id,
      batchNumber: v.batchNumber,
      expiryDate: lastDayOfMonth(v.expiryMonth),
      mrp: v.mrp,
      sellingPrice: v.sellingPrice,
      purchaseRate: v.purchaseRate,
      quantity: v.quantity,
      supplierName: v.supplierName || null,
      supplierInvoiceNo: v.supplierInvoiceNo || null
    }).subscribe({
      next: () => {
        this.saving = false;
        this.receiving = false;
        this.receiveForm.reset();
        this.load();
      },
      error: err => {
        this.saving = false;
        this.receiveError = err.error?.message || 'Could not receive stock.';
      }
    });
  }

  startAdjust(b: Batch): void {
    this.adjusting = b;
    this.adjustError = '';
    this.adjustForm.reset();
  }

  adjust(): void {
    if (!this.adjusting || this.adjustForm.invalid) return;
    const v = this.adjustForm.getRawValue();
    this.saving = true;
    this.adjustError = '';

    this.service.adjust(this.adjusting.id, v.quantityChange, v.reason).subscribe({
      next: () => {
        this.saving = false;
        this.adjusting = null;
        this.load();
      },
      error: err => {
        this.saving = false;
        this.adjustError = err.error?.message || 'Could not adjust stock.';
      }
    });
  }

  deactivate(): void {
    if (!this.medicine || !confirm(`Deactivate ${this.medicine.name}? It will be hidden from search.`)) return;
    this.service.deactivate(this.medicine.id).subscribe(() => this.router.navigate(['/medicines']));
  }
}

/** Indian packs print expiry as month/year; a batch is good until the end of that month. */
function lastDayOfMonth(yyyyMm: string): string {
  const [y, m] = yyyyMm.split('-').map(Number);
  const day = new Date(y, m, 0).getDate();
  return `${yyyyMm}-${String(day).padStart(2, '0')}`;
}
