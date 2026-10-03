import { Component } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth.service';
import { UserRole } from './models';

interface NavItem {
  label: string;
  path: string;
  roles: UserRole[];
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar no-print">
      <a class="brand" routerLink="/"><img src="amaran-mark-navy.svg" alt="" class="logo"> Pharmacy</a>
      <nav>
        @for (item of visibleNav(); track item.path) {
          <a [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: item.path === '/' }">{{ item.label }}</a>
        }
      </nav>
      <span class="user">
        {{ auth.currentUser()?.fullName }} <span class="role">{{ auth.currentUser()?.role }}</span>
        <button class="btn-link" (click)="logout()">Log out</button>
      </span>
    </header>
    <main><router-outlet></router-outlet></main>
  `,
  styles: [`
    .topbar { display: flex; align-items: center; gap: 1.5rem; padding: .75rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); }
    .brand { display: flex; align-items: center; gap: .5rem; font-weight: 700; color: var(--brand); font-size: 1.1rem; text-decoration: none; }
    .logo { height: 28px; width: auto; }
    nav { display: flex; gap: 1rem; flex: 1; }
    nav a { color: var(--text); text-decoration: none; padding: .25rem 0; }
    nav a.active { color: var(--brand); border-bottom: 2px solid var(--brand); }
    .user { display: flex; align-items: center; gap: .6rem; font-size: .9rem; }
    .role { background: #ccfbf1; color: var(--brand-dark); border-radius: 999px; padding: .1rem .55rem; font-size: .75rem; }
    /* Use the screen width on desktops, with a comfortable gutter; cap it on very wide monitors */
    main { padding: 1.5rem clamp(1rem, 3vw, 2.5rem); max-width: 1600px; margin: 0 auto; }
  `]
})
export class ShellComponent {
  private nav: NavItem[] = [
    { label: 'Dashboard', path: '/', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Counter', path: '/pos', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Sales', path: '/sales', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Prescriptions', path: '/prescriptions', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Patients', path: '/patients', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Medicines', path: '/medicines', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Stock alerts', path: '/stock-alerts', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Purchasing', path: '/purchase-orders', roles: ['Admin', 'Pharmacist', 'Technician'] },
    { label: 'Reports', path: '/reports', roles: ['Admin', 'Pharmacist'] },
    { label: 'H1 register', path: '/register', roles: ['Admin', 'Pharmacist'] },
    { label: 'Audit log', path: '/audit', roles: ['Admin'] },
    { label: 'Staff', path: '/users', roles: ['Admin'] },
    { label: 'Settings', path: '/settings', roles: ['Admin'] }
  ];

  constructor(public auth: AuthService, private router: Router) {}

  visibleNav(): NavItem[] {
    return this.nav.filter(item => this.auth.hasRole(...item.roles));
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
