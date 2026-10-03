import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { PO_STATUSES, PurchaseOrderStatus, PurchaseOrderSummary, poStatusLabel } from './purchasing.models';
import { PurchasingService } from './purchasing.service';

@Component({
  selector: 'app-purchase-order-list',
  standalone: true,
  imports: [RouterLink, DatePipe, CurrencyPipe],
  template: `
    <div class="head">
      <h1>Purchase orders</h1>
      @if (canBuy) {
        <div class="actions">
          <a class="btn-link" routerLink="/suppliers">Suppliers</a>
          <a class="btn" routerLink="/purchase-orders/new">New purchase order</a>
        </div>
      }
    </div>

    <div class="card">
      <div class="tabs">
        <button [class.active]="status === null" (click)="filter(null)">All</button>
        @for (s of statuses; track s.value) {
          <button [class.active]="status === s.value" (click)="filter(s.value)">{{ s.label }}</button>
        }
      </div>
      <table>
        <thead><tr><th>PO</th><th>Supplier</th><th>Created</th><th>Items</th><th>Units received</th><th class="num">Est. value</th><th>Status</th></tr></thead>
        <tbody>
          @for (p of orders; track p.id) {
            <tr class="row" [routerLink]="['/purchase-orders', p.id]">
              <td><strong>{{ p.poNumber }}</strong></td>
              <td>{{ p.supplierName }}</td>
              <td>{{ p.createdAt | date: 'd MMM yyyy' }}</td>
              <td>{{ p.lineCount }}</td>
              <td>{{ p.unitsReceived }} / {{ p.unitsOrdered }}</td>
              <td class="num">{{ p.estimatedValue === null ? '—' : (p.estimatedValue | currency: 'INR') }}</td>
              <td><span class="badge" [class]="'badge ' + p.status.toLowerCase()">{{ label(p.status) }}</span></td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="muted">No purchase orders.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    .actions { display: flex; gap: 1.25rem; align-items: center; }
    a.btn { text-decoration: none; }
    .tabs { display: flex; gap: .5rem; margin-bottom: .75rem; flex-wrap: wrap; }
    .tabs button { border: 1px solid var(--border); background: var(--surface); border-radius: 999px; padding: .3rem .9rem; cursor: pointer; }
    .tabs button.active { background: var(--brand); border-color: var(--brand); color: #fff; }
    .row { cursor: pointer; }
    .row:hover { background: #f0fdfa; }
    .num { text-align: right; }
    .draft { background: #e5e7eb; }
    .ordered { background: #dbeafe; color: #1e40af; }
    .partiallyreceived { background: #fef3c7; color: #92400e; }
    .received { background: #ccfbf1; color: var(--brand-dark); }
    .closed, .cancelled { background: #f3f4f6; color: var(--muted); }
  `]
})
export class PurchaseOrderListComponent implements OnInit {
  orders: PurchaseOrderSummary[] = [];
  statuses = PO_STATUSES;
  status: PurchaseOrderStatus | null = null;
  canBuy = this.auth.hasRole('Admin', 'Pharmacist');
  label = poStatusLabel;

  constructor(private service: PurchasingService, private auth: AuthService) {}

  ngOnInit(): void {
    // Counter staff mostly need what's on its way, to receive it
    this.filter(this.canBuy ? null : 'Ordered');
  }

  filter(status: PurchaseOrderStatus | null): void {
    this.status = status;
    this.service.orders(status).subscribe(list => this.orders = list);
  }
}
