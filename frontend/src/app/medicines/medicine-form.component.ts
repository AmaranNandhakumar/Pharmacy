import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DOSAGE_FORMS, DRUG_SCHEDULES, DrugSchedule, GST_RATES, Medicine, MedicineDetail, MedicineRequest } from './medicine.models';
import { MedicineService } from './medicine.service';

/** Create or edit a medicine. Emits the saved medicine. */
@Component({
  selector: 'app-medicine-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="save()">
      <h2>{{ medicine ? 'Edit medicine' : 'Add medicine' }}</h2>
      <div class="grid">
        <label class="field">Brand name * <input formControlName="name"></label>
        <label class="field">Generic name <input formControlName="genericName" placeholder="e.g. Paracetamol"></label>
        <label class="field">Strength <input formControlName="strength" placeholder="e.g. 500 mg"></label>
        <label class="field">Form *
          <select formControlName="form">
            @for (f of forms; track f) { <option [value]="f">{{ f }}</option> }
          </select>
        </label>
        <label class="field">Pack size <input formControlName="packSize" placeholder="e.g. 10 tablets"></label>
        <label class="field">Manufacturer <input formControlName="manufacturer"></label>
        <label class="field">Barcode <input formControlName="barcode"></label>
        <label class="field">Schedule *
          <select formControlName="schedule">
            @for (s of schedules; track s.value) { <option [value]="s.value">{{ s.label }}</option> }
          </select>
        </label>
        <label class="field">HSN code * <input formControlName="hsnCode" placeholder="3004"></label>
        <label class="field">GST rate *
          <select formControlName="gstRatePercent">
            @for (r of gstRates; track r) { <option [ngValue]="r">{{ r }}%</option> }
          </select>
        </label>
        <label class="field">Reorder level <input type="number" min="0" formControlName="reorderLevel"></label>
      </div>
      @if (form.controls.hsnCode.touched && form.controls.hsnCode.invalid) {
        <p class="error">HSN code must be 4, 6 or 8 digits.</p>
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
    .actions { display: flex; gap: 1rem; align-items: center; }
  `]
})
export class MedicineFormComponent implements OnInit {
  @Input() medicine: Medicine | null = null;
  @Output() saved = new EventEmitter<MedicineDetail>();
  @Output() cancelled = new EventEmitter<void>();

  forms = DOSAGE_FORMS;
  schedules = DRUG_SCHEDULES;
  gstRates = GST_RATES;
  saving = false;
  error = '';

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    genericName: [''],
    strength: [''],
    form: ['Tablet', Validators.required],
    packSize: [''],
    manufacturer: [''],
    barcode: [''],
    schedule: ['H' as DrugSchedule, Validators.required],
    hsnCode: ['3004', [Validators.required, Validators.pattern(/^\d{4}(\d{2}){0,2}$/)]],
    gstRatePercent: [5, Validators.required],
    reorderLevel: [10, [Validators.required, Validators.min(0)]]
  });

  constructor(private fb: FormBuilder, private service: MedicineService) {}

  ngOnInit(): void {
    if (this.medicine) {
      const m = this.medicine;
      this.form.setValue({
        name: m.name,
        genericName: m.genericName ?? '',
        strength: m.strength ?? '',
        form: m.form,
        packSize: m.packSize ?? '',
        manufacturer: m.manufacturer ?? '',
        barcode: m.barcode ?? '',
        schedule: m.schedule,
        hsnCode: m.hsnCode,
        gstRatePercent: m.gstRatePercent,
        reorderLevel: m.reorderLevel
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';

    const v = this.form.getRawValue();
    const req: MedicineRequest = {
      ...v,
      genericName: v.genericName || null,
      strength: v.strength || null,
      packSize: v.packSize || null,
      manufacturer: v.manufacturer || null,
      barcode: v.barcode || null
    };
    const call = this.medicine ? this.service.update(this.medicine.id, req) : this.service.create(req);

    call.subscribe({
      next: m => {
        this.saving = false;
        this.saved.emit(m);
      },
      error: err => {
        this.saving = false;
        this.error = err.error?.message || 'Could not save the medicine.';
      }
    });
  }
}
