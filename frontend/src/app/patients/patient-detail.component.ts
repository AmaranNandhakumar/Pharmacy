import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PatientDetail } from './patient.models';
import { PatientFormComponent } from './patient-form.component';
import { PatientService } from './patient.service';
import { StatusBadgeComponent } from '../prescriptions/status-badge.component';

@Component({
  selector: 'app-patient-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, PatientFormComponent, StatusBadgeComponent],
  template: `
    <a routerLink="/patients" class="back">← Patients</a>

    @if (patient; as p) {
      @if (editing) {
        <app-patient-form [patient]="p" (saved)="patient = $event; editing = false" (cancelled)="editing = false"></app-patient-form>
      } @else {
        <div class="card info">
          <div class="title">
            <div>
              <h1>{{ p.fullName }}</h1>
              <p class="muted">{{ p.age }} years · born {{ p.dateOfBirth | date: 'd MMM yyyy' }} {{ p.phone ? '· ' + p.phone : '' }}</p>
            </div>
            <button class="btn" (click)="editing = true">Edit</button>
          </div>
          <div class="facts">
            <span>Allergies:
              @if (p.allergies) { <span class="badge rx">{{ p.allergies }}</span> } @else { <span class="muted">none recorded</span> }
            </span>
            @if (p.address) { <span>{{ p.address }}</span> }
            @if (p.notes) { <span class="muted">{{ p.notes }}</span> }
            <span class="muted small">Consent recorded {{ p.consentGivenAt | date: 'd MMM yyyy' }}</span>
          </div>
        </div>
      }

      <div class="card">
        <div class="title">
          <h2>Prescriptions</h2>
          <a class="btn" [routerLink]="['/prescriptions/new']" [queryParams]="{ patientId: p.id }">New prescription</a>
        </div>
        <table>
          <thead><tr><th>#</th><th>Issued</th><th>Prescriber</th><th>Items</th><th>Status</th></tr></thead>
          <tbody>
            @for (rx of p.prescriptions; track rx.id) {
              <tr class="row" [routerLink]="['/prescriptions', rx.id]">
                <td>{{ rx.id }}</td>
                <td>{{ rx.issuedOn | date: 'd MMM yyyy' }}</td>
                <td>{{ rx.prescriberName }}</td>
                <td>{{ rx.itemCount }}</td>
                <td><app-status-badge [status]="rx.status"></app-status-badge></td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="muted">No prescriptions yet.</td></tr>
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
    a.btn { text-decoration: none; }
    .facts { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-top: .75rem; font-size: .9rem; align-items: center; }
    .small { font-size: .8rem; }
    .row { cursor: pointer; }
    .row:hover { background: #f0fdfa; }
    app-patient-form { display: block; margin-bottom: 1.25rem; }
  `]
})
export class PatientDetailComponent implements OnInit {
  patient: PatientDetail | null = null;
  editing = false;
  loadError = '';

  constructor(private route: ActivatedRoute, private service: PatientService) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.get(id).subscribe({
      next: p => this.patient = p,
      error: err => this.loadError = err.status === 404 ? 'Patient not found.' : 'Could not load the patient.'
    });
  }
}
