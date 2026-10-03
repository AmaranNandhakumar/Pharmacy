import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, of, switchMap } from 'rxjs';
import { Medicine } from '../medicines/medicine.models';
import { MedicineService } from '../medicines/medicine.service';
import { ReorderSuggestion, Supplier } from './purchasing.models';
import { PurchasingService } from './purchasing.service';

interface DraftLine {
  medicineId: number;
  name: string;
  quantity: number;
  expectedRate: number | null;
}

/** Draft a purchase order: pick from the low-stock suggestions or search, then save as a draft. */
@Component({
  selector: 'app-purchase-order-form',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <a [routerLink]="editId ? ['/purchase-orders', editId] : ['/purchase-orders']" class="back">← Back</a>
    <h1>{{ editId ? 'Edit draft purchase order' : 'New purchase order' }}</h1>

    <div class="layout">
      <div>
        <div class="card">
          <label class="field">Supplier *
            <select [(ngModel)]="supplierId">
              <option [ngValue]="null" disabled>Choose a supplier</option>
              @for (s of suppliers; track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
            </select>
          </label>
          @if (suppliers.length === 0) { <p class="muted">No suppliers yet. <a routerLink="/suppliers">Add one first.</a></p> }
          <label class="field">Notes <input [(ngModel)]="notes" placeholder="e.g. deliver before Monday"></label>
        </div>

        <div class="card">
          <div class="title">
            <h2>Low stock</h2>
            @if (suggestions.length) {
              <button class="btn-link" (click)="addAllSuggestions()">Add {{ supplierId ? 'this supplier\\'s' : 'all' }}</button>
            }
          </div>
          <table>
            <thead><tr><th></th><th>Medicine</th><th class="num">In stock</th><th class="num">Reorder at</th><th class="num">On order</th><th class="num">Suggest</th><th>Last supplier</th></tr></thead>
            <tbody>
              @for (s of suggestions; track s.medicineId) {
                <tr [class.other]="supplierId && s.lastSupplierId && s.lastSupplierId !== supplierId">
                  <td><button class="btn-link" (click)="addSuggestion(s)" [disabled]="has(s.medicineId)">{{ has(s.medicineId) ? 'Added' : 'Add' }}</button></td>
                  <td>{{ s.name }} {{ s.strength }} <span class="muted small">{{ s.form }}</span></td>
                  <td class="num low">{{ s.sellableQuantity }}</td>
                  <td class="num">{{ s.reorderLevel }}</td>
                  <td class="num">{{ s.onOrder || '' }}</td>
                  <td class="num"><strong>{{ s.suggestedQuantity }}</strong></td>
                  <td class="muted">{{ s.lastSupplierName ?? '—' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="muted">Nothing is below its reorder level.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <h2>Order lines</h2>
        <input class="search" placeholder="Add any medicine: search by name" [(ngModel)]="query" (ngModelChange)="search$.next(query)">
        <ul class="results">
          @for (m of results; track m.id) {
            <li (click)="addMedicine(m)">{{ m.name }} {{ m.strength }} <span class="muted small">{{ m.sellableQuantity }} in stock</span></li>
          }
        </ul>
        <table>
          <thead><tr><th>Medicine</th><th>Units</th><th>Rate ₹</th><th></th></tr></thead>
          <tbody>
            @for (l of lines; track l.medicineId; let i = $index) {
              <tr>
                <td>{{ l.name }}</td>
                <td><input type="number" min="1" [(ngModel)]="l.quantity" class="qty"></td>
                <td><input type="number" min="0" step="0.01" [(ngModel)]="l.expectedRate" class="qty" placeholder="—"></td>
                <td><button class="btn-link danger" (click)="lines.splice(i, 1)">✕</button></td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="muted">Add medicines from the low-stock list or search.</td></tr>
            }
          </tbody>
        </table>
        @if (estimate() !== null) { <p class="total">Estimated value: <strong>₹{{ estimate()!.toFixed(2) }}</strong></p> }
        @if (error) { <p class="error">{{ error }}</p> }
        <button class="btn wide" (click)="save()" [disabled]="saving || !supplierId || lines.length === 0 || !validLines()">Save draft</button>
        <p class="muted small">You can still change a draft. Mark it as ordered once it's sent to the supplier.</p>
      </div>
    </div>
  `,
  styles: [`
    .back { display: inline-block; margin-bottom: 1rem; color: var(--brand); text-decoration: none; }
    .layout { display: grid; grid-template-columns: 1fr 420px; gap: 1.25rem; align-items: start; }
    @media (max-width: 1000px) { .layout { grid-template-columns: 1fr; } }
    .card { margin-bottom: 1.25rem; }
    .title { display: flex; justify-content: space-between; align-items: center; }
    .search { width: 100%; padding: .5rem .65rem; border: 1px solid var(--border); border-radius: 6px; }
    .results { list-style: none; padding: 0; margin: .25rem 0 .75rem; }
    .results li { padding: .4rem .5rem; cursor: pointer; border-bottom: 1px solid var(--border); }
    .results li:hover { background: #f0fdfa; }
    .qty { width: 5.5rem; padding: .3rem; border: 1px solid var(--border); border-radius: 6px; }
    .num { text-align: right; }
    .low { color: var(--danger); font-weight: 600; }
    .other td { opacity: .5; }
    .small { font-size: .8rem; }
    .danger { color: var(--danger); }
    .total { text-align: right; }
    .wide { width: 100%; margin-top: .5rem; }
    td a, p a { color: var(--brand); }
  `]
})
export class PurchaseOrderFormComponent implements OnInit {
  editId: number | null = null;
  suppliers: Supplier[] = [];
  suggestions: ReorderSuggestion[] = [];
  supplierId: number | null = null;
  notes = '';
  lines: DraftLine[] = [];

  query = '';
  results: Medicine[] = [];
  search$ = new Subject<string>();
  saving = false;
  error = '';

  constructor(
    private service: PurchasingService,
    private medicines: MedicineService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.service.suppliers().subscribe(list => this.suppliers = list);
    this.service.suggestions().subscribe(list => this.suggestions = list);
    this.search$.pipe(
      debounceTime(250),
      switchMap(q => q.trim().length < 2 ? of([]) : this.medicines.search(q))
    ).subscribe(list => this.results = list);

    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      this.editId = id;
      this.service.order(id).subscribe(po => {
        this.supplierId = po.supplierId;
        this.notes = po.notes ?? '';
        this.lines = po.lines.map(l => ({
          medicineId: l.medicineId, name: `${l.medicineName} ${l.strength ?? ''}`.trim(), quantity: l.quantityOrdered, expectedRate: l.expectedRate
        }));
      });
    }
  }

  has(medicineId: number): boolean {
    return this.lines.some(l => l.medicineId === medicineId);
  }

  addSuggestion(s: ReorderSuggestion): void {
    if (this.has(s.medicineId)) return;
    this.supplierId ??= s.lastSupplierId;
    this.lines.push({ medicineId: s.medicineId, name: `${s.name} ${s.strength ?? ''}`.trim(), quantity: s.suggestedQuantity, expectedRate: s.lastPurchaseRate });
  }

  /** Adds every suggestion, or only the chosen supplier's (plus ones with no known supplier). */
  addAllSuggestions(): void {
    this.suggestions
      .filter(s => !this.supplierId || !s.lastSupplierId || s.lastSupplierId === this.supplierId)
      .forEach(s => this.addSuggestion(s));
  }

  addMedicine(m: Medicine): void {
    this.query = '';
    this.results = [];
    if (this.has(m.id)) return;
    const s = this.suggestions.find(x => x.medicineId === m.id);
    this.lines.push({ medicineId: m.id, name: `${m.name} ${m.strength ?? ''}`.trim(), quantity: s?.suggestedQuantity ?? Math.max(m.reorderLevel, 1), expectedRate: s?.lastPurchaseRate ?? null });
  }

  validLines(): boolean {
    return this.lines.every(l => l.quantity >= 1 && (l.expectedRate === null || l.expectedRate >= 0));
  }

  estimate(): number | null {
    if (this.lines.length === 0 || this.lines.some(l => l.expectedRate === null)) return null;
    return this.lines.reduce((sum, l) => sum + (l.expectedRate ?? 0) * l.quantity, 0);
  }

  save(): void {
    this.saving = true;
    this.error = '';
    const req = {
      supplierId: this.supplierId!,
      notes: this.notes.trim() || null,
      lines: this.lines.map(l => ({ medicineId: l.medicineId, quantity: l.quantity, expectedRate: l.expectedRate ?? null }))
    };
    const call = this.editId ? this.service.update(this.editId, req) : this.service.create(req);
    call.subscribe({
      next: po => this.router.navigate(['/purchase-orders', po.id]),
      error: err => { this.saving = false; this.error = err.error?.message || 'Could not save the purchase order.'; }
    });
  }
}
