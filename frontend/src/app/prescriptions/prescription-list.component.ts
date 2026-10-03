import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { PRESCRIPTION_STATUSES, PrescriptionStatus, PrescriptionSummary } from './prescription.models';
import { PrescriptionService } from './prescription.service';
import { StatusBadgeComponent } from './status-badge.component';

@Component({
  selector: 'app-prescription-list',
  standalone: true,
  imports: [RouterLink, DatePipe, StatusBadgeComponent],
  template: `
    <div class="head">
      <h1>Prescriptions</h1>
      <a class="btn" routerLink="/prescriptions/new">New prescription</a>
    </div>

    <div class="card">
      <div class="tabs">
        <button [class.active]="status === null" (click)="filter(null)">All</button>
        @for (s of statuses; track s) {
          <button [class.active]="status === s" (click)="filter(s)">{{ s }}</button>
        }
      </div>
      @if (error) { <p class="error">{{ error }}</p> }
      <table>
        <thead><tr><th>#</th><th>Patient</th><th>Prescriber</th><th>Issued</th><th>Items</th><th>Status</th></tr></thead>
        <tbody>
          @for (rx of prescriptions; track rx.id) {
            <tr class="row" [routerLink]="['/prescriptions', rx.id]">
              <td>{{ rx.id }}</td>
              <td><strong>{{ rx.patientName }}</strong></td>
              <td>{{ rx.prescriberName }}</td>
              <td>{{ rx.issuedOn | date: 'd MMM yyyy' }}</td>
              <td>{{ rx.itemCount }}</td>
              <td><app-status-badge [status]="rx.status"></app-status-badge></td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No prescriptions.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    a.btn { text-decoration: none; }
    .tabs { display: flex; gap: .5rem; margin-bottom: .75rem; flex-wrap: wrap; }
    .tabs button { border: 1px solid var(--border); background: var(--surface); border-radius: 999px; padding: .3rem .9rem; cursor: pointer; }
    .tabs button.active { background: var(--brand); border-color: var(--brand); color: #fff; }
    .row { cursor: pointer; }
    .row:hover { background: #f0fdfa; }
  `]
})
export class PrescriptionListComponent implements OnInit {
  prescriptions: PrescriptionSummary[] = [];
  statuses = PRESCRIPTION_STATUSES;
  status: PrescriptionStatus | null = null;
  error = '';

  constructor(private service: PrescriptionService, private auth: AuthService) {}

  ngOnInit(): void {
    // Pharmacists land on their work queue: prescriptions waiting to be verified
    this.filter(this.auth.hasRole('Pharmacist') ? 'Entered' : null);
  }

  filter(status: PrescriptionStatus | null): void {
    this.status = status;
    this.service.list(status).subscribe({
      next: list => { this.prescriptions = list; this.error = ''; },
      error: () => this.error = 'Could not load prescriptions.'
    });
  }
}
