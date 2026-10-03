import { Component, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { scheduleLabel } from '../medicines/medicine.models';
import { DispenseResult, Prescription } from './prescription.models';
import { PrescriptionService } from './prescription.service';
import { StatusBadgeComponent } from './status-badge.component';

@Component({
  selector: 'app-prescription-detail',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, CurrencyPipe, StatusBadgeComponent],
  template: `
    <a routerLink="/prescriptions" class="back">← Prescriptions</a>

    @if (rx; as r) {
      <div class="card">
        <div class="title">
          <div>
            <h1>Prescription #{{ r.id }} <app-status-badge [status]="r.status"></app-status-badge></h1>
            <p class="muted">
              For <a [routerLink]="['/patients', r.patientId]">{{ r.patientName }}</a>
              · {{ r.prescriberName }} ({{ r.prescriberRegNo }}) · issued {{ r.issuedOn | date: 'd MMM yyyy' }}
            </p>
          </div>
        </div>
        <div class="facts">
          <span>Allergies:
            @if (r.patientAllergies) { <span class="badge rx">{{ r.patientAllergies }}</span> } @else { <span class="muted">none recorded</span> }
          </span>
          <span>Entered by {{ r.enteredByName }}</span>
          @if (r.verifiedByName) { <span>Verified by {{ r.verifiedByName }}, {{ r.verifiedAt | date: 'd MMM, h:mm a' }}</span> }
          @if (r.dispensedByName) { <span>Last dispensed by {{ r.dispensedByName }}, {{ r.dispensedAt | date: 'd MMM, h:mm a' }}</span> }
          @if (r.copyRetained) { <span>Copy retained</span> }
        </div>
        @if (r.status === 'Rejected') { <p class="error">Rejected: {{ r.rejectReason }}</p> }
      </div>

      @if (r.hasAllergyWarnings && (r.status === 'Entered' || r.status === 'Verified')) {
        <div class="card warning">
          <strong>Allergy warning.</strong> This patient's recorded allergies match:
          <ul>
            @for (i of r.items; track i.id) {
              @for (a of i.allergyWarnings; track a) { <li>{{ i.medicineName }} matches "{{ a }}"</li> }
            }
          </ul>
        </div>
      }

      <div class="card">
        <h2>Medicines</h2>
        <table>
          <thead><tr><th>Medicine</th><th>Dose and directions</th><th>Qty</th><th>Refills</th><th>In stock</th></tr></thead>
          <tbody>
            @for (i of r.items; track i.id) {
              <tr>
                <td>
                  <strong>{{ i.medicineName }}</strong> {{ i.strength }}
                  <span class="badge" [class.rx]="i.schedule !== 'Otc' && i.schedule !== 'G'">{{ label(i.schedule) }}</span>
                  @if (i.genericName) { <div class="muted small">{{ i.genericName }}</div> }
                </td>
                <td>{{ i.dose }} · {{ i.directions }}</td>
                <td>{{ i.quantity }}</td>
                <td>{{ i.refillsUsed }} of {{ i.refillsAllowed }} used</td>
                <td [class.low]="i.sellableQuantity < i.quantity">{{ i.sellableQuantity }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @if (isPharmacist) {
        <div class="card actions">
          @if (r.status === 'Entered') {
            @if (r.hasAllergyWarnings) {
              <label class="check"><input type="checkbox" [(ngModel)]="acknowledge"> I have reviewed the allergy warning and it is safe to dispense</label>
            }
            <button class="btn" (click)="verify()" [disabled]="busy || (r.hasAllergyWarnings && !acknowledge)">Verify</button>
          }
          @if (r.status === 'Verified' || canRefill(r)) {
            <button class="btn" (click)="dispense()" [disabled]="busy">{{ r.status === 'Dispensed' ? 'Dispense refill' : 'Dispense' }}</button>
          }
          @if (r.status === 'Entered' || r.status === 'Verified') {
            <div class="reject">
              <input placeholder="Reason for rejecting" [(ngModel)]="rejectReason">
              <button class="btn-link danger" (click)="reject()" [disabled]="busy || rejectReason.trim().length < 3">Reject</button>
            </div>
          }
        </div>
      } @else if (r.status === 'Entered') {
        <p class="muted">Waiting for a pharmacist to verify.</p>
      }
      @if (error) { <p class="error">{{ error }}</p> }

      @if (result) {
        <div class="card">
          <div class="title">
            <h2>{{ result.wasRefill ? 'Refill handed over' : 'Handed over' }}</h2>
            <a class="btn" [routerLink]="['/pos']" [queryParams]="{ fillId: result.fillId }">Bill at the counter</a>
          </div>
          <p class="muted">Picked from the earliest-expiring batches first.</p>
          <table>
            <thead><tr><th>Medicine</th><th>Batch</th><th>Expiry</th><th>MRP</th><th>Qty</th></tr></thead>
            <tbody>
              @for (l of result.lines; track $index) {
                <tr>
                  <td>{{ l.medicineName }}</td>
                  <td>{{ l.batchNumber }}</td>
                  <td>{{ l.expiryDate | date: 'MMM yyyy' }}</td>
                  <td>{{ l.mrp | currency: 'INR' }}</td>
                  <td>{{ l.quantity }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    } @else if (loadError) {
      <p class="error">{{ loadError }}</p>
    }
  `,
  styles: [`
    .back { display: inline-block; margin-bottom: 1rem; color: var(--brand); text-decoration: none; }
    .card { margin-bottom: 1.25rem; }
    h1 { margin-bottom: .25rem; display: flex; gap: .75rem; align-items: center; }
    .title { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
    .title a:not(.btn), p a { color: var(--brand); }
    a.btn { text-decoration: none; }
    .facts { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-top: .75rem; font-size: .9rem; align-items: center; }
    .warning { background: #fef2f2; border-color: #fecaca; color: var(--danger); }
    .warning ul { margin: .5rem 0 0; }
    .small { font-size: .8rem; }
    .low { color: var(--danger); font-weight: 600; }
    .actions { display: flex; gap: 1rem; align-items: center; flex-wrap: wrap; }
    .check { display: flex; gap: .5rem; font-size: .9rem; flex-basis: 100%; }
    .reject { display: flex; gap: .5rem; align-items: center; margin-left: auto; }
    .reject input { padding: .45rem .6rem; border: 1px solid var(--border); border-radius: 6px; min-width: 220px; }
    .danger { color: var(--danger); }
  `]
})
export class PrescriptionDetailComponent implements OnInit {
  rx: Prescription | null = null;
  result: DispenseResult | null = null;
  isPharmacist = this.auth.hasRole('Pharmacist');
  acknowledge = false;
  rejectReason = '';
  busy = false;
  error = '';
  loadError = '';
  label = scheduleLabel;

  constructor(private route: ActivatedRoute, private service: PrescriptionService, private auth: AuthService) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.get(id).subscribe({
      next: rx => this.rx = rx,
      error: err => this.loadError = err.status === 404 ? 'Prescription not found.' : 'Could not load the prescription.'
    });
  }

  canRefill(r: Prescription): boolean {
    return r.status === 'Dispensed' && r.items.some(i => i.refillsUsed < i.refillsAllowed);
  }

  verify(): void {
    this.run(this.service.verify(this.rx!.id, this.acknowledge), rx => this.rx = rx);
  }

  reject(): void {
    this.run(this.service.reject(this.rx!.id, this.rejectReason.trim()), rx => this.rx = rx);
  }

  dispense(): void {
    this.run(this.service.dispense(this.rx!.id), res => { this.result = res; this.rx = res.prescription; });
  }

  private run<T>(call: Observable<T>, done: (value: T) => void): void {
    this.busy = true;
    this.error = '';
    call.subscribe({
      next: value => { this.busy = false; done(value); },
      error: err => { this.busy = false; this.error = err.error?.message || 'Something went wrong. Try again.'; }
    });
  }
}
