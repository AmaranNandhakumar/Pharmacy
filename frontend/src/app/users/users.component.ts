import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { USER_ROLES, User, UserRole } from '../core/models';
import { UserService } from './user.service';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  template: `
    <h1>Staff</h1>

    <form class="card add" [formGroup]="form" (ngSubmit)="create()">
      <h2>Add staff member</h2>
      <div class="grid">
        <label class="field">Full name <input formControlName="fullName"></label>
        <label class="field">Email <input type="email" formControlName="email"></label>
        <label class="field">Temporary password <input type="password" formControlName="password" autocomplete="new-password"></label>
        <label class="field">Role
          <select formControlName="role">
            @for (r of roles; track r) { <option [value]="r">{{ r }}</option> }
          </select>
        </label>
      </div>
      @if (formError) { <p class="error">{{ formError }}</p> }
      <button class="btn" type="submit" [disabled]="form.invalid || saving">Add</button>
      <span class="muted hint">Passwords need at least 8 characters.</span>
    </form>

    <div class="card">
      @if (listError) { <p class="error">{{ listError }}</p> }
      <table>
        <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Status</th><th>Added</th><th></th></tr></thead>
        <tbody>
          @for (u of users; track u.id) {
            <tr [class.inactive]="!u.isActive">
              <td>{{ u.fullName }}</td>
              <td>{{ u.email }}</td>
              <td>
                <select [value]="u.role" (change)="changeRole(u, $any($event.target).value)">
                  @for (r of roles; track r) { <option [value]="r">{{ r }}</option> }
                </select>
              </td>
              <td>{{ u.isActive ? 'Active' : 'Deactivated' }}</td>
              <td>{{ u.createdAt | date: 'mediumDate' }}</td>
              <td><button class="btn-link" (click)="toggleActive(u)">{{ u.isActive ? 'Deactivate' : 'Reactivate' }}</button></td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No staff yet.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .add { margin-bottom: 1.25rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0 1rem; }
    .hint { margin-left: .75rem; font-size: .85rem; }
    tr.inactive td { color: var(--muted); }
  `]
})
export class UsersComponent implements OnInit {
  users: User[] = [];
  roles = USER_ROLES;
  saving = false;
  formError = '';
  listError = '';

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    role: ['Technician' as UserRole, Validators.required]
  });

  constructor(private fb: FormBuilder, private userService: UserService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.userService.getAll().subscribe({
      next: users => this.users = users,
      error: () => this.listError = 'Could not load staff.'
    });
  }

  create(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.formError = '';

    this.userService.create(this.form.getRawValue()).subscribe({
      next: () => {
        this.saving = false;
        this.form.reset();
        this.load();
      },
      error: err => {
        this.saving = false;
        this.formError = err.error?.message || 'Could not add staff member.';
      }
    });
  }

  changeRole(user: User, role: UserRole): void {
    this.listError = '';
    this.userService.update(user.id, user.fullName, role).subscribe({
      next: () => this.load(),
      error: err => {
        this.listError = err.error?.message || 'Could not change role.';
        this.load();
      }
    });
  }

  toggleActive(user: User): void {
    this.listError = '';
    this.userService.setActive(user.id, !user.isActive).subscribe({
      next: () => this.load(),
      error: err => this.listError = err.error?.message || 'Could not update status.'
    });
  }
}
