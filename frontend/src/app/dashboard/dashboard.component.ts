import { Component } from '@angular/core';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <h1>Welcome, {{ auth.currentUser()?.fullName }}</h1>
    <div class="card">
      <p>You're signed in as <strong>{{ auth.currentUser()?.role }}</strong>.</p>
      <p class="muted">Stock alerts, today's prescriptions and sales will appear here as later milestones land.</p>
    </div>
  `
})
export class DashboardComponent {
  constructor(public auth: AuthService) {}
}
