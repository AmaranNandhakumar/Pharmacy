import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SaleSummary } from './sale.models';
import { SaleService, localDate } from './sale.service';

@Component({
  selector: 'app-sales-list',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, CurrencyPipe],
  template: `
    <div class="head">
      <h1>Sales</h1>
      <a class="btn" routerLink="/pos">New bill</a>
    </div>

    <div class="card">
      <div class="toolbar">
        <label>From <input type="date" [(ngModel)]="from" (change)="load()" [max]="to"></label>
        <label>To <input type="date" [(ngModel)]="to" (change)="load()" [min]="from"></label>
        <span class="summary">
          {{ completed().length }} bills · <strong>{{ takings() | currency: 'INR' }}</strong> taken
          @if (voidedCount()) { <span class="muted">· {{ voidedCount() }} voided</span> }
        </span>
      </div>
      @if (error) { <p class="error">{{ error }}</p> }
      <table>
        <thead><tr><th>Invoice</th><th>Time</th><th>Customer</th><th>Items</th><th>Payment</th><th class="num">Total</th></tr></thead>
        <tbody>
          @for (s of sales; track s.id) {
            <tr class="row" [class.voided]="s.status === 'Voided'" [routerLink]="['/sales', s.id]">
              <td><strong>{{ s.invoiceNo }}</strong> @if (s.status === 'Voided') { <span class="badge rx">Voided</span> }</td>
              <td>{{ s.createdAt | date: 'd MMM, h:mm a' }}</td>
              <td>{{ s.customerName || 'Cash customer' }}</td>
              <td>{{ s.itemCount }}</td>
              <td>{{ s.paymentMethod === 'Upi' ? 'UPI' : s.paymentMethod }}</td>
              <td class="num">{{ s.total | currency: 'INR' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No sales in this period.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    a.btn { text-decoration: none; }
    .toolbar { display: flex; gap: 1rem; align-items: center; margin-bottom: .75rem; flex-wrap: wrap; }
    .toolbar input { padding: .4rem .5rem; border: 1px solid var(--border); border-radius: 6px; margin-left: .3rem; }
    .summary { margin-left: auto; }
    .num { text-align: right; }
    .row { cursor: pointer; }
    .row:hover { background: #f0fdfa; }
    .voided td { color: var(--muted); text-decoration: line-through; }
    .voided td:first-child { text-decoration: none; }
  `]
})
export class SalesListComponent implements OnInit {
  sales: SaleSummary[] = [];
  from = localDate();
  to = localDate();
  error = '';

  constructor(private service: SaleService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.list(this.from, this.to).subscribe({
      next: list => { this.sales = list; this.error = ''; },
      error: () => this.error = 'Could not load sales.'
    });
  }

  completed(): SaleSummary[] {
    return this.sales.filter(s => s.status === 'Completed');
  }

  takings(): number {
    return this.completed().reduce((sum, s) => sum + s.total, 0);
  }

  voidedCount(): number {
    return this.sales.length - this.completed().length;
  }
}
