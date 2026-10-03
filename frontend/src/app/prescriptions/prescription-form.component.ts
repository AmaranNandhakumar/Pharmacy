import { Component, OnInit } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, of, switchMap } from 'rxjs';
import { Medicine, scheduleLabel } from '../medicines/medicine.models';
import { MedicineService } from '../medicines/medicine.service';
import { Patient } from '../patients/patient.models';
import { PatientService } from '../patients/patient.service';
import { PrescriptionItemRequest, PrescriptionRequest } from './prescription.models';
import { PrescriptionService } from './prescription.service';

/** Enter a paper prescription at the counter. A pharmacist verifies it afterwards. */
@Component({
  selector: 'app-prescription-form',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, RouterLink],
  template: `
    <a routerLink="/prescriptions" class="back">← Prescriptions</a>
    <h1>New prescription</h1>

    <div class="card">
      <h2>Patient</h2>
      @if (patient; as p) {
        <div class="picked">
          <div>
            <strong>{{ p.fullName }}</strong> <span class="muted">{{ p.age }} years {{ p.phone ? '· ' + p.phone : '' }}</span>
            <div>Allergies:
              @if (p.allergies) { <span class="badge rx">{{ p.allergies }}</span> } @else { <span class="muted">none recorded</span> }
            </div>
          </div>
          <button class="btn-link" type="button" (click)="patient = null">Change</button>
        </div>
      } @else {
        <input class="search" placeholder="Search patient by name or mobile" [(ngModel)]="patientQuery" (ngModelChange)="patientSearch$.next(patientQuery)">
        <ul class="results">
          @for (p of patientResults; track p.id) {
            <li (click)="pickPatient(p)"><strong>{{ p.fullName }}</strong> <span class="muted">{{ p.age }} yrs {{ p.phone ?? '' }}</span></li>
          }
        </ul>
        <p class="muted small">Not registered yet? <a routerLink="/patients">Add the patient first</a>.</p>
      }
    </div>

    <form [formGroup]="form" (ngSubmit)="save()">
      <div class="card">
        <h2>Prescriber</h2>
        <div class="grid">
          <label class="field">Doctor's name * <input formControlName="prescriberName" placeholder="Dr. ..."></label>
          <label class="field">Registration no. * <input formControlName="prescriberRegNo" placeholder="NMC / state council no."></label>
          <label class="field">Date on prescription * <input type="date" formControlName="issuedOn" [max]="today"></label>
          <label class="field wide">Clinic address <input formControlName="prescriberAddress"></label>
        </div>
        <label class="check"><input type="checkbox" formControlName="copyRetained"> Pharmacy has kept a copy (required for Schedule X)</label>
      </div>

      <div class="card">
        <h2>Medicines</h2>
        <input class="search" placeholder="Add a medicine: search by brand or generic name" [(ngModel)]="medicineQuery"
               [ngModelOptions]="{ standalone: true }" (ngModelChange)="medicineSearch$.next(medicineQuery)">
        <ul class="results">
          @for (m of medicineResults; track m.id) {
            <li (click)="addItem(m)">
              <strong>{{ m.name }}</strong> {{ m.strength }} <span class="muted">{{ m.genericName }}</span>
              <span class="badge" [class.rx]="m.requiresPrescription">{{ label(m.schedule) }}</span>
              <span class="muted small">{{ m.sellableQuantity }} in stock</span>
            </li>
          }
        </ul>

        <table formArrayName="items">
          <thead><tr><th>Medicine</th><th>Dose</th><th>Qty</th><th>Directions</th><th>Refills</th><th></th></tr></thead>
          <tbody>
            @for (row of items.controls; track row; let i = $index) {
              <tr [formGroupName]="i">
                <td><strong>{{ names[i] }}</strong></td>
                <td><input formControlName="dose" placeholder="1 tablet"></td>
                <td><input type="number" min="1" formControlName="quantity" class="num"></td>
                <td><input formControlName="directions" placeholder="Twice a day after food"></td>
                <td><input type="number" min="0" max="12" formControlName="refillsAllowed" class="num"></td>
                <td><button class="btn-link danger" type="button" (click)="removeItem(i)">Remove</button></td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="muted">Search above to add the prescribed medicines.</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (error) { <p class="error">{{ error }}</p> }
      <button class="btn" type="submit" [disabled]="!patient || form.invalid || items.length === 0 || saving">Save for verification</button>
    </form>
  `,
  styles: [`
    .back { display: inline-block; margin-bottom: 1rem; color: var(--brand); text-decoration: none; }
    .card { margin-bottom: 1.25rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0 1rem; }
    .wide { grid-column: 1 / -1; }
    .check { display: flex; gap: .5rem; font-size: .9rem; }
    .search { width: 100%; padding: .55rem .7rem; border: 1px solid var(--border); border-radius: 6px; font-size: .95rem; }
    .results { list-style: none; padding: 0; margin: .25rem 0 1rem; }
    .results li { padding: .45rem .5rem; cursor: pointer; border-bottom: 1px solid var(--border); display: flex; gap: .5rem; align-items: center; }
    .results li:hover { background: #f0fdfa; }
    .picked { display: flex; justify-content: space-between; align-items: flex-start; gap: .5rem; }
    .picked > div { display: flex; flex-direction: column; gap: .35rem; }
    td input { width: 100%; padding: .35rem .45rem; border: 1px solid var(--border); border-radius: 6px; }
    td input.num { width: 4.5rem; }
    .danger { color: var(--danger); }
    .small { font-size: .8rem; }
  `]
})
export class PrescriptionFormComponent implements OnInit {
  patient: Patient | null = null;
  patientQuery = '';
  patientResults: Patient[] = [];
  patientSearch$ = new Subject<string>();

  medicineQuery = '';
  medicineResults: Medicine[] = [];
  medicineSearch$ = new Subject<string>();

  /** Display names for the item rows, in the same order as the form array. */
  names: string[] = [];
  today = new Date().toISOString().slice(0, 10);
  saving = false;
  error = '';
  label = scheduleLabel;

  form = this.fb.nonNullable.group({
    prescriberName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    prescriberRegNo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
    prescriberAddress: [''],
    issuedOn: [this.today, Validators.required],
    copyRetained: [false],
    items: this.fb.array<FormGroup>([])
  });

  get items(): FormArray<FormGroup> {
    return this.form.controls.items;
  }

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private patients: PatientService,
    private medicines: MedicineService,
    private service: PrescriptionService
  ) {}

  ngOnInit(): void {
    const patientId = Number(this.route.snapshot.queryParamMap.get('patientId'));
    if (patientId) this.patients.get(patientId).subscribe({ next: p => this.patient = p });

    this.patientSearch$.pipe(
      debounceTime(250),
      switchMap(q => q.trim().length < 2 ? of([]) : this.patients.search(q))
    ).subscribe(list => this.patientResults = list);

    this.medicineSearch$.pipe(
      debounceTime(250),
      switchMap(q => q.trim().length < 2 ? of([]) : this.medicines.search(q))
    ).subscribe(list => this.medicineResults = list.filter(m => m.schedule !== 'Ndps'));
  }

  pickPatient(p: Patient): void {
    this.patient = p;
    this.patientQuery = '';
    this.patientResults = [];
  }

  addItem(m: Medicine): void {
    this.medicineQuery = '';
    this.medicineResults = [];
    if (this.items.controls.some(c => c.value.medicineId === m.id)) return;

    this.items.push(this.itemGroup(m.id));
    this.names.push(`${m.name} ${m.strength ?? ''}`.trim());
  }

  removeItem(i: number): void {
    this.items.removeAt(i);
    this.names.splice(i, 1);
  }

  save(): void {
    if (!this.patient || this.form.invalid) return;
    this.saving = true;
    this.error = '';

    const v = this.form.getRawValue();
    const req: PrescriptionRequest = {
      ...v,
      patientId: this.patient.id,
      items: v.items as PrescriptionItemRequest[],
      prescriberAddress: v.prescriberAddress || null
    };

    this.service.create(req).subscribe({
      next: rx => this.router.navigate(['/prescriptions', rx.id]),
      error: err => {
        this.saving = false;
        this.error = err.error?.message || 'Could not save the prescription.';
      }
    });
  }

  private itemGroup(medicineId: number) {
    return this.fb.nonNullable.group({
      medicineId: [medicineId],
      dose: ['1 tablet', [Validators.required, Validators.maxLength(100)]],
      quantity: [10, [Validators.required, Validators.min(1), Validators.max(10000)]],
      directions: ['', [Validators.required, Validators.maxLength(300)]],
      refillsAllowed: [0, [Validators.required, Validators.min(0), Validators.max(12)]]
    });
  }
}
