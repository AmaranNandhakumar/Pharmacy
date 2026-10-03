import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { StockAlerts } from '../medicines/medicine.models';
import { MedicineService } from '../medicines/medicine.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-stock-alerts',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, CurrencyPipe],
  template: `
    <div class="head">
      <h1>Stock alerts</h1>
      <label class="muted">Expiring within
        <select [(ngModel)]="days" (ngModelChange)="load()">
          @for (d of dayOptions; track d) { <option [ngValue]="d">{{ d }} days</option> }
        </select>
      </label>
    </div>
    @if (error) { <p class="error">{{ error }}</p> }

    @if (alerts; as a) {
      <section class="card">
        <h2>Expired <span class="count danger">{{ a.expired.length }}</span></h2>
        <p class="muted">Remove these from the shelf; they can't be sold.</p>
        <table>
          <thead><tr><th>Medicine</th><th>Batch</th><th>Expired</th><th>Units</th><th>MRP</th></tr></thead>
          <tbody>
            @for (b of a.expired; track b.id) {
              <tr><td><a [routerLink]="['/medicines', b.medicineId]">{{ b.medicineName }}</a></td><td>{{ b.batchNumber }}</td>
                <td>{{ b.expiryDate | date: 'MMM yyyy' }}</td><td>{{ b.quantityOnHand }}</td><td>{{ b.mrp | currency: 'INR' }}</td></tr>
            } @empty { <tr><td colspan="5" class="muted">Nothing expired on the shelf.</td></tr> }
          </tbody>
        </table>
      </section>

      <section class="card">
        <h2>Expiring soon <span class="count warn">{{ a.expiringSoon.length }}</span></h2>
        <table>
          <thead><tr><th>Medicine</th><th>Batch</th><th>Expires</th><th>Units</th><th>MRP</th></tr></thead>
          <tbody>
            @for (b of a.expiringSoon; track b.id) {
              <tr><td><a [routerLink]="['/medicines', b.medicineId]">{{ b.medicineName }}</a></td><td>{{ b.batchNumber }}</td>
                <td>{{ b.expiryDate | date: 'd MMM yyyy' }}</td><td>{{ b.quantityOnHand }}</td><td>{{ b.mrp | currency: 'INR' }}</td></tr>
            } @empty { <tr><td colspan="5" class="muted">Nothing expires in the next {{ a.expiringWithinDays }} days.</td></tr> }
          </tbody>
        </table>
      </section>

      <section class="card">
        <h2>Low stock <span class="count warn">{{ a.lowStock.length }}</span>
          @if (canBuy && a.lowStock.length) { <a class="btn-link order" routerLink="/purchase-orders/new">Create purchase order</a> }
        </h2>
        <table>
          <thead><tr><th>Medicine</th><th>Sellable</th><th>Reorder level</th></tr></thead>
          <tbody>
            @for (i of a.lowStock; track i.medicineId) {
              <tr><td><a [routerLink]="['/medicines', i.medicineId]">{{ i.name }} {{ i.strength }}</a> <span class="muted">{{ i.form }}</span></td>
                <td [class.out]="i.sellableQuantity === 0">{{ i.sellableQuantity === 0 ? 'Out of stock' : i.sellableQuantity }}</td><td>{{ i.reorderLevel }}</td></tr>
            } @empty { <tr><td colspan="3" class="muted">Everything is above its reorder level.</td></tr> }
          </tbody>
        </table>
      </section>
    }
  `,
  styles: [`
    .order { font-size: .9rem; font-weight: 400; margin-left: 1rem; text-decoration: none; }
    .head { display: flex; justify-content: space-between; align-items: center; }
    .head select { margin-left: .4rem; padding: .3rem; }
    section { margin-bottom: 1.25rem; }
    h2 { display: flex; align-items: center; gap: .5rem; }
    .count { font-size: .8rem; border-radius: 999px; padding: .1rem .55rem; color: #fff; }
    .danger { background: var(--danger); }
    .warn { background: #d97706; }
    .out { color: var(--danger); font-weight: 600; }
    a { color: var(--brand); text-decoration: none; }
  `]
})
export class StockAlertsComponent implements OnInit {
  alerts: StockAlerts | null = null;
  days = 90;
  dayOptions = [30, 60, 90, 180];
  error = '';

  canBuy = this.auth.hasRole('Admin', 'Pharmacist');

  constructor(private service: MedicineService, private auth: AuthService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.alerts(this.days).subscribe({
      next: a => { this.alerts = a; this.error = ''; },
      error: () => this.error = 'Could not load stock alerts.'
    });
  }
}
