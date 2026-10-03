import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { Patient, PatientDetail } from './patient.models';
import { PatientFormComponent } from './patient-form.component';
import { PatientService } from './patient.service';

@Component({
  selector: 'app-patient-list',
  standalone: true,
  imports: [FormsModule, PatientFormComponent],
  template: `
    <div class="head">
      <h1>Patients</h1>
      <button class="btn" (click)="adding = !adding">{{ adding ? 'Close' : 'Add patient' }}</button>
    </div>

    @if (adding) {
      <app-patient-form (saved)="created($event)" (cancelled)="adding = false"></app-patient-form>
    }

    <div class="card">
      <input class="search" placeholder="Search by name or mobile number" [(ngModel)]="query" (ngModelChange)="search$.next()" autofocus>
      @if (error) { <p class="error">{{ error }}</p> }
      <table>
        <thead><tr><th>Name</th><th>Age</th><th>Mobile</th><th>Allergies</th></tr></thead>
        <tbody>
          @for (p of patients; track p.id) {
            <tr class="row" (click)="open(p)">
              <td><strong>{{ p.fullName }}</strong></td>
              <td>{{ p.age }}</td>
              <td>{{ p.phone ?? '—' }}</td>
              <td>@if (p.allergies) { <span class="badge rx">{{ p.allergies }}</span> } @else { <span class="muted">None recorded</span> }</td>
            </tr>
          } @empty {
            <tr><td colspan="4" class="muted">No patients found.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    app-patient-form { display: block; margin-bottom: 1.25rem; }
    .search { width: 100%; padding: .55rem .7rem; border: 1px solid var(--border); border-radius: 6px; font-size: .95rem; margin-bottom: .75rem; }
    .row { cursor: pointer; }
    .row:hover { background: #f0fdfa; }
  `]
})
export class PatientListComponent implements OnInit {
  patients: Patient[] = [];
  query = '';
  adding = false;
  error = '';
  search$ = new Subject<void>();

  constructor(private service: PatientService, private router: Router) {}

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(250),
      switchMap(() => this.service.search(this.query))
    ).subscribe({
      next: list => { this.patients = list; this.error = ''; },
      error: () => this.error = 'Could not load patients.'
    });
    this.search$.next();
  }

  open(p: Patient): void {
    this.router.navigate(['/patients', p.id]);
  }

  created(p: PatientDetail): void {
    this.adding = false;
    this.router.navigate(['/patients', p.id]);
  }
}
