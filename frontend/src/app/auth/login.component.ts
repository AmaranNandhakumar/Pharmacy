import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="wrap">
      <form class="card" [formGroup]="form" (ngSubmit)="submit()">
        <img src="amaran-mark-navy.svg" alt="" class="logo">
        <h1>Pharmacy</h1>
        <p class="muted">Sign in with your staff account.</p>

        <label class="field">Email
          <input type="email" formControlName="email" autocomplete="username">
        </label>
        <label class="field">Password
          <input type="password" formControlName="password" autocomplete="current-password">
        </label>

        @if (errorMessage) { <p class="error">{{ errorMessage }}</p> }

        <button class="btn" type="submit" [disabled]="form.invalid || loading">
          {{ loading ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
    </div>
  `,
  styles: [`
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 1rem; }
    form { width: 100%; max-width: 360px; }
    .logo { display: block; height: 56px; margin: 0 auto .75rem; }
    h1 { color: var(--brand); margin-bottom: .25rem; text-align: center; }
    form > .muted { text-align: center; }
    .btn { width: 100%; }
  `]
})
export class LoginComponent {
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  errorMessage = '';
  loading = false;

  constructor(private fb: FormBuilder, private authService: AuthService, private router: Router) {}

  submit(): void {
    if (this.form.invalid) return;

    this.loading = true;
    this.errorMessage = '';
    const { email, password } = this.form.getRawValue();

    this.authService.login(email, password).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || 'Sign-in failed. Please try again.';
      }
    });
  }
}
