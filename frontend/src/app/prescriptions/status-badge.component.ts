import { Component, Input } from '@angular/core';
import { PrescriptionStatus } from './prescription.models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span [class]="'badge ' + status.toLowerCase()">{{ status }}</span>`,
  styles: [`
    .entered { background: #fef3c7; color: #92400e; }
    .verified { background: #dbeafe; color: #1e40af; }
    .dispensed { background: #ccfbf1; color: var(--brand-dark); }
    .rejected { background: #fee2e2; color: var(--danger); }
  `]
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: PrescriptionStatus;
}
