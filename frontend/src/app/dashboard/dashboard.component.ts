import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { StockAlerts } from '../medicines/medicine.models';
import { MedicineService } from '../medicines/medicine.service';
import { PrescriptionService } from '../prescriptions/prescription.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <h1>Welcome, {{ auth.currentUser()?.fullName }}</h1>
    <div class="tiles">
      <a class="card tile" routerLink="/prescriptions">
        <span class="num warn">{{ awaitingVerification ?? '–' }}</span>
        <span>Prescriptions waiting for a pharmacist</span>
      </a>
      <a class="card tile" routerLink="/prescriptions">
        <span class="num">{{ readyToDispense ?? '–' }}</span>
        <span>Verified, ready to dispense</span>
      </a>
      <a class="card tile" routerLink="/stock-alerts">
        <span class="num danger">{{ alerts?.expired?.length ?? '–' }}</span>
        <span>Expired batches on the shelf</span>
      </a>
      <a class="card tile" routerLink="/stock-alerts">
        <span class="num warn">{{ alerts?.expiringSoon?.length ?? '–' }}</span>
        <span>Batches expiring in 90 days</span>
      </a>
      <a class="card tile" routerLink="/stock-alerts">
        <span class="num warn">{{ alerts?.lowStock?.length ?? '–' }}</span>
        <span>Medicines at or below reorder level</span>
      </a>
    </div>
    <p class="muted">Signed in as {{ auth.currentUser()?.role }}. Sales arrive in the next milestone.</p>
  `,
  styles: [`
    .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-bottom: 1rem; }
    .tile { display: flex; flex-direction: column; gap: .35rem; text-decoration: none; color: var(--text); }
    .tile:hover { border-color: var(--brand); }
    .num { font-size: 2rem; font-weight: 700; color: var(--brand); }
    .danger { color: var(--danger); }
    .warn { color: #d97706; }
  `]
})
export class DashboardComponent implements OnInit {
  alerts: StockAlerts | null = null;
  awaitingVerification: number | null = null;
  readyToDispense: number | null = null;

  constructor(public auth: AuthService, private medicines: MedicineService, private prescriptions: PrescriptionService) {}

  ngOnInit(): void {
    this.medicines.alerts(90).subscribe({ next: a => this.alerts = a });
    this.prescriptions.list('Entered').subscribe({ next: list => this.awaitingVerification = list.length });
    this.prescriptions.list('Verified').subscribe({ next: list => this.readyToDispense = list.length });
  }
}
