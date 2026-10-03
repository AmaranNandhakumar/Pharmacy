import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Supplier, SupplierRequest } from './purchasing.models';
import { PurchasingService } from './purchasing.service';

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, RouterLink],
  template: `
    <div class="head">
      <h1>Suppliers</h1>
      <button class="btn" (click)="startAdd()">{{ editing === 'new' ? 'Close' : 'Add supplier' }}</button>
    </div>

    @if (editing !== null) {
      <form class="card" [formGroup]="form" (ngSubmit)="save()">
        <h2>{{ editing === 'new' ? 'Add supplier' : 'Edit supplier' }}</h2>
        <div class="grid">
          <label class="field">Name * <input formControlName="name"></label>
          <label class="field">Contact person <input formControlName="contactPerson"></label>
          <label class="field">Phone <input formControlName="phone"></label>
          <label class="field">Email <input type="email" formControlName="email"></label>
          <label class="field">GSTIN <input formControlName="gstin" placeholder="33ABCDE1234F1Z5"></label>
          <label class="field">Wholesale drug licence (20B / 21B) <input formControlName="drugLicenceNo"></label>
          <label class="field wide">Address <input formControlName="address"></label>
        </div>
        @if (form.controls.gstin.touched && form.controls.gstin.invalid) { <p class="error">GSTIN is 15 characters, e.g. 33ABCDE1234F1Z5.</p> }
        @if (error) { <p class="error">{{ error }}</p> }
        <div class="actions">
          <button class="btn" type="submit" [disabled]="form.invalid || saving">Save</button>
          <button class="btn-link" type="button" (click)="editing = null">Cancel</button>
        </div>
      </form>
    }

    <div class="card">
      <label class="muted"><input type="checkbox" [(ngModel)]="includeInactive" (change)="load()"> Show inactive</label>
      <table>
        <thead><tr><th>Supplier</th><th>Contact</th><th>GSTIN</th><th>Drug licence</th><th>Open orders</th><th></th></tr></thead>
        <tbody>
          @for (s of suppliers; track s.id) {
            <tr [class.inactive]="!s.isActive">
              <td><strong>{{ s.name }}</strong>@if (s.address) { <div class="muted small">{{ s.address }}</div> }</td>
              <td>{{ s.contactPerson }} <div class="muted small">{{ s.phone }} {{ s.email }}</div></td>
              <td>{{ s.gstin ?? '—' }}</td>
              <td>{{ s.drugLicenceNo ?? '—' }}</td>
              <td>@if (s.openOrders) { <a routerLink="/purchase-orders">{{ s.openOrders }}</a> } @else { 0 }</td>
              <td class="row-actions">
                @if (s.isActive) {
                  <button class="btn-link" (click)="startEdit(s)">Edit</button>
                  <button class="btn-link danger" (click)="deactivate(s)">Deactivate</button>
                } @else { <span class="muted">Inactive</span> }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No suppliers yet. Add the distributors you buy from.</td></tr>
          }
        </tbody>
      </table>
      @if (listError) { <p class="error">{{ listError }}</p> }
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    form.card { margin-bottom: 1.25rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0 1rem; }
    .wide { grid-column: 1 / -1; }
    .actions, .row-actions { display: flex; gap: 1rem; align-items: center; }
    .small { font-size: .8rem; }
    .inactive td { color: var(--muted); }
    .danger { color: var(--danger); }
    td a { color: var(--brand); }
  `]
})
export class SuppliersComponent implements OnInit {
  suppliers: Supplier[] = [];
  includeInactive = false;
  editing: 'new' | Supplier | null = null;
  saving = false;
  error = '';
  listError = '';

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
    contactPerson: [''],
    phone: [''],
    email: ['', Validators.email],
    gstin: ['', Validators.pattern(/^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/)],
    drugLicenceNo: [''],
    address: ['']
  });

  constructor(private fb: FormBuilder, private service: PurchasingService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.suppliers(this.includeInactive).subscribe(list => this.suppliers = list);
  }

  startAdd(): void {
    this.editing = this.editing === 'new' ? null : 'new';
    this.form.reset();
    this.error = '';
  }

  startEdit(s: Supplier): void {
    this.editing = s;
    this.error = '';
    this.form.setValue({
      name: s.name, contactPerson: s.contactPerson ?? '', phone: s.phone ?? '', email: s.email ?? '',
      gstin: s.gstin ?? '', drugLicenceNo: s.drugLicenceNo ?? '', address: s.address ?? ''
    });
  }

  save(): void {
    this.saving = true;
    this.error = '';
    const v = this.form.getRawValue();
    const req: SupplierRequest = {
      name: v.name,
      contactPerson: v.contactPerson || null,
      phone: v.phone || null,
      email: v.email || null,
      gstin: v.gstin ? v.gstin.toUpperCase() : null,
      drugLicenceNo: v.drugLicenceNo || null,
      address: v.address || null
    };
    const call = this.editing === 'new' || this.editing === null
      ? this.service.createSupplier(req)
      : this.service.updateSupplier(this.editing.id, req);
    call.subscribe({
      next: () => { this.saving = false; this.editing = null; this.load(); },
      error: err => { this.saving = false; this.error = err.error?.message || 'Could not save the supplier.'; }
    });
  }

  deactivate(s: Supplier): void {
    if (!confirm(`Deactivate ${s.name}? Past purchase orders keep pointing at them.`)) return;
    this.listError = '';
    this.service.deactivateSupplier(s.id).subscribe({
      next: () => this.load(),
      error: err => this.listError = err.error?.message || 'Could not deactivate the supplier.'
    });
  }
}
