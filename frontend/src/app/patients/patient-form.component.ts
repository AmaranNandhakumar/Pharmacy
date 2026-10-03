import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Patient, PatientDetail, PatientRequest } from './patient.models';
import { PatientService } from './patient.service';

/** Create or edit a patient. Emits the saved patient. */
@Component({
  selector: 'app-patient-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="save()">
      <h2>{{ patient ? 'Edit patient' : 'Add patient' }}</h2>
      <div class="grid">
        <label class="field">Full name * <input formControlName="fullName"></label>
        <label class="field">Date of birth * <input type="date" formControlName="dateOfBirth" [max]="today"></label>
        <label class="field">Mobile <input formControlName="phone" placeholder="98765 43210"></label>
        <label class="field wide">Address <input formControlName="address"></label>
        <label class="field wide">Allergies <input formControlName="allergies" placeholder="Separate with commas, e.g. penicillin, sulfa"></label>
        <label class="field wide">Notes <input formControlName="notes"></label>
      </div>
      @if (form.controls.phone.touched && form.controls.phone.invalid) {
        <p class="error">Enter a 10-digit Indian mobile number.</p>
      }
      @if (!patient) {
        <label class="consent">
          <input type="checkbox" formControlName="consentGiven">
          The patient agrees to the pharmacy keeping these details to fill their prescriptions (DPDP Act 2023).
        </label>
      }
      @if (error) { <p class="error">{{ error }}</p> }
      <div class="actions">
        <button class="btn" type="submit" [disabled]="form.invalid || saving">Save</button>
        <button class="btn-link" type="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0 1rem; }
    .wide { grid-column: 1 / -1; }
    .consent { display: flex; gap: .5rem; align-items: flex-start; margin-bottom: 1rem; font-size: .9rem; }
    .actions { display: flex; gap: 1rem; align-items: center; }
  `]
})
export class PatientFormComponent implements OnInit {
  @Input() patient: Patient | null = null;
  @Output() saved = new EventEmitter<PatientDetail>();
  @Output() cancelled = new EventEmitter<void>();

  today = new Date().toISOString().slice(0, 10);
  saving = false;
  error = '';

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    dateOfBirth: ['', Validators.required],
    phone: ['', Validators.pattern(/^(\+91[\- ]?)?[6-9]\d{4}[\- ]?\d{5}$/)],
    address: [''],
    allergies: [''],
    notes: [''],
    consentGiven: [false, Validators.requiredTrue]
  });

  constructor(private fb: FormBuilder, private service: PatientService) {}

  ngOnInit(): void {
    if (this.patient) {
      const p = this.patient;
      this.form.setValue({
        fullName: p.fullName,
        dateOfBirth: p.dateOfBirth,
        phone: p.phone ?? '',
        address: p.address ?? '',
        allergies: p.allergies ?? '',
        notes: p.notes ?? '',
        // Consent was recorded when the patient was created
        consentGiven: true
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';

    const v = this.form.getRawValue();
    const req: PatientRequest = {
      ...v,
      phone: v.phone.replace(/[\s-]/g, '') || null,
      address: v.address || null,
      allergies: v.allergies || null,
      notes: v.notes || null
    };
    const call = this.patient ? this.service.update(this.patient.id, req) : this.service.create(req);

    call.subscribe({
      next: p => {
        this.saving = false;
        this.saved.emit(p);
      },
      error: err => {
        this.saving = false;
        this.error = err.error?.message || 'Could not save the patient.';
      }
    });
  }
}
