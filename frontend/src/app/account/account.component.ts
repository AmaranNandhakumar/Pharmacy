import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { passwordPolicy } from '../core/password-policy';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';

/** Signed-in staff: change your own password, or sign out on every device. */
@Component({
  selector: 'app-account',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <h1>My account</h1>
    <div class="layout">
      <div class="card">
        <h2>{{ auth.currentUser()?.fullName }}</h2>
        <p class="muted">{{ auth.currentUser()?.email }} · {{ auth.currentUser()?.role }}</p>
        <p class="muted small">Signed-in sessions renew themselves for up to 7 days of inactivity. Use this if you
          signed in on a shared or lost device.</p>
        <button class="btn-link danger" (click)="logoutEverywhere()">Sign out on all devices</button>
      </div>

      <form class="card" [formGroup]="form" (ngSubmit)="save()">
        <h2>Change password</h2>
        <label class="field">Current password
          <input type="password" formControlName="currentPassword" autocomplete="current-password">
        </label>
        <label class="field">New password
          <input type="password" formControlName="newPassword" autocomplete="new-password">
        </label>
        @if (form.controls.newPassword.touched && form.controls.newPassword.errors?.['policy']; as msg) {
          <p class="error">{{ msg }}</p>
        }
        <label class="field">Repeat new password
          <input type="password" formControlName="confirm" autocomplete="new-password">
        </label>
        @if (form.controls.confirm.touched && mismatch()) { <p class="error">The new passwords don't match.</p> }
        @if (error) { <p class="error">{{ error }}</p> }
        @if (done) { <p class="ok">Password changed. Your other devices have been signed out.</p> }
        <button class="btn" type="submit" [disabled]="form.invalid || mismatch() || saving">Change password</button>
      </form>
    </div>
  `,
  styles: [`
    .layout { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1.25rem; align-items: start; max-width: 900px; }
    .small { font-size: .85rem; }
    .danger { color: var(--danger); }
    .ok { color: var(--brand); }
  `]
})
export class AccountComponent {
  form = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, passwordPolicy]],
    confirm: ['', Validators.required]
  });
  saving = false;
  done = false;
  error = '';

  constructor(private fb: FormBuilder, public auth: AuthService, private router: Router) {}

  mismatch(): boolean {
    const v = this.form.getRawValue();
    return !!v.confirm && v.confirm !== v.newPassword;
  }

  save(): void {
    this.saving = true;
    this.done = false;
    this.error = '';
    const v = this.form.getRawValue();
    this.auth.changePassword(v.currentPassword, v.newPassword).subscribe({
      next: () => { this.saving = false; this.done = true; this.form.reset(); },
      error: err => { this.saving = false; this.error = err.error?.message || 'Could not change the password.'; }
    });
  }

  logoutEverywhere(): void {
    if (!confirm('Sign out on every device, including this one?')) return;
    this.auth.logoutEverywhere().subscribe({ complete: () => this.router.navigate(['/login']), error: () => this.router.navigate(['/login']) });
  }
}
