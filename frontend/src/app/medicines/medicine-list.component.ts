import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { Medicine, MedicineDetail, scheduleLabel } from './medicine.models';
import { MedicineFormComponent } from './medicine-form.component';
import { MedicineService } from './medicine.service';

@Component({
  selector: 'app-medicine-list',
  standalone: true,
  imports: [FormsModule, DatePipe, MedicineFormComponent],
  template: `
    <div class="head">
      <h1>Medicines</h1>
      @if (canEdit) {
        <button class="btn" (click)="adding = !adding">{{ adding ? 'Close' : 'Add medicine' }}</button>
      }
    </div>

    @if (adding) {
      <app-medicine-form (saved)="created($event)" (cancelled)="adding = false"></app-medicine-form>
    }

    <div class="card">
      <div class="toolbar">
        <input class="search" placeholder="Search by brand, generic name or barcode" [(ngModel)]="query" (ngModelChange)="search$.next()" autofocus>
        <label class="muted"><input type="checkbox" [(ngModel)]="includeInactive" (ngModelChange)="search$.next()"> Show inactive</label>
      </div>
      @if (error) { <p class="error">{{ error }}</p> }
      <table>
        <thead><tr><th>Medicine</th><th>Schedule</th><th>GST</th><th>In stock</th><th>Nearest expiry</th></tr></thead>
        <tbody>
          @for (m of medicines; track m.id) {
            <tr class="row" [class.inactive]="!m.isActive" (click)="open(m)">
              <td>
                <strong>{{ m.name }}</strong> {{ m.strength }} <span class="muted">{{ m.form }}</span>
                @if (m.genericName) { <div class="muted small">{{ m.genericName }}</div> }
              </td>
              <td><span class="badge" [class.rx]="m.requiresPrescription">{{ label(m.schedule) }}</span></td>
              <td>{{ m.gstRatePercent }}%</td>
              <td [class.low]="m.sellableQuantity <= m.reorderLevel">{{ m.sellableQuantity }}</td>
              <td>{{ m.nearestExpiry ? (m.nearestExpiry | date: 'MMM yyyy') : '—' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="muted">No medicines found.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    app-medicine-form { display: block; margin-bottom: 1.25rem; }
    .toolbar { display: flex; gap: 1rem; align-items: center; margin-bottom: .75rem; }
    .search { flex: 1; padding: .55rem .7rem; border: 1px solid var(--border); border-radius: 6px; font-size: .95rem; }
    .row { cursor: pointer; }
    .row:hover { background: #f0fdfa; }
    .inactive td { color: var(--muted); }
    .small { font-size: .8rem; }
    .low { color: var(--danger); font-weight: 600; }
  `]
})
export class MedicineListComponent implements OnInit {
  medicines: Medicine[] = [];
  query = '';
  includeInactive = false;
  adding = false;
  error = '';
  canEdit = this.auth.hasRole('Admin', 'Pharmacist');
  search$ = new Subject<void>();
  label = scheduleLabel;

  constructor(private service: MedicineService, private auth: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(250),
      switchMap(() => this.service.search(this.query, this.includeInactive))
    ).subscribe({
      next: list => { this.medicines = list; this.error = ''; },
      error: () => this.error = 'Could not load medicines.'
    });
    this.search$.next();
  }

  open(m: Medicine): void {
    this.router.navigate(['/medicines', m.id]);
  }

  created(m: MedicineDetail): void {
    this.adding = false;
    this.router.navigate(['/medicines', m.id]);
  }
}
