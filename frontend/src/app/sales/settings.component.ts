import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PharmacySettings } from './sale.models';
import { SaleService } from './sale.service';

/** The pharmacy's own details printed on every invoice (Admin only). */
@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <h1>Pharmacy details</h1>
    <form class="card" [formGroup]="form" (ngSubmit)="save()">
      <p class="muted">These print at the top of every invoice. The placeholders are fake: replace them with your licence details.</p>
      <div class="grid">
        <label class="field wide">Pharmacy name * <input formControlName="name"></label>
        <label class="field wide">Address * <input formControlName="address"></label>
        <label class="field">Phone <input formControlName="phone"></label>
        <label class="field">GST state code * <input formControlName="stateCode" placeholder="33"></label>
        <label class="field">GSTIN * <input formControlName="gstin" placeholder="33ABCDE1234F1Z5"></label>
        <label class="field">Drug licence (Form 20) * <input formControlName="drugLicence20"></label>
        <label class="field">Drug licence (Form 21) * <input formControlName="drugLicence21"></label>
        <label class="field">Registered pharmacist * <input formControlName="registeredPharmacistName"></label>
        <label class="field">Pharmacist reg. no. * <input formControlName="registeredPharmacistRegNo"></label>
      </div>
      @if (form.controls.gstin.touched && form.controls.gstin.invalid) {
        <p class="error">GSTIN is 15 characters, e.g. 33ABCDE1234F1Z5.</p>
      }
      @if (error) { <p class="error">{{ error }}</p> }
      @if (saved) { <p class="ok">Saved.</p> }
      <button class="btn" type="submit" [disabled]="form.invalid || saving">Save</button>
    </form>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0 1rem; }
    .wide { grid-column: 1 / -1; }
    .ok { color: var(--brand); }
  `]
})
export class SettingsComponent implements OnInit {
  saving = false;
  saved = false;
  error = '';

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    address: ['', Validators.required],
    phone: [''],
    stateCode: ['', [Validators.required, Validators.pattern(/^\d{2}$/)]],
    gstin: ['', [Validators.required, Validators.pattern(/^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/)]],
    drugLicence20: ['', Validators.required],
    drugLicence21: ['', Validators.required],
    registeredPharmacistName: ['', Validators.required],
    registeredPharmacistRegNo: ['', Validators.required]
  });

  constructor(private fb: FormBuilder, private service: SaleService) {}

  ngOnInit(): void {
    this.service.settings().subscribe(s => this.form.setValue({ ...s, phone: s.phone ?? '' }));
  }

  save(): void {
    this.saving = true;
    this.saved = false;
    this.error = '';
    const v = this.form.getRawValue();
    const req: PharmacySettings = { ...v, gstin: v.gstin.toUpperCase(), phone: v.phone || null };
    this.service.updateSettings(req).subscribe({
      next: () => { this.saving = false; this.saved = true; },
      error: err => { this.saving = false; this.error = err.error?.message || 'Could not save.'; }
    });
  }
}
