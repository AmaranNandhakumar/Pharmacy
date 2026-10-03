import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RegisterEntry } from './sale.models';
import { SaleService, localDate } from './sale.service';

/** Schedule H1 / X register, filled in from each sale. Printable for a drug inspector's visit. */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="head">
      <h1>Schedule {{ schedule }} register</h1>
      <button class="btn no-print" (click)="print()">Print</button>
    </div>
    <div class="card">
      <div class="toolbar no-print">
        <label><input type="radio" name="sch" value="H1" [(ngModel)]="schedule" (change)="load()"> Schedule H1</label>
        <label><input type="radio" name="sch" value="X" [(ngModel)]="schedule" (change)="load()"> Schedule X</label>
        <label>From <input type="date" [(ngModel)]="from" (change)="load()"></label>
        <label>To <input type="date" [(ngModel)]="to" (change)="load()"></label>
      </div>
      @if (error) { <p class="error">{{ error }}</p> }
      <table>
        <thead>
          <tr><th>Date</th><th>Invoice</th><th>Patient (name, address)</th><th>Prescriber (name, reg. no., address)</th>
              <th>Drug</th><th>Batch</th><th>Qty</th><th>Pharmacist</th></tr>
        </thead>
        <tbody>
          @for (e of entries; track e.id) {
            <tr>
              <td>{{ e.createdAt | date: 'd MMM yyyy' }}</td>
              <td>{{ e.invoiceNo }}</td>
              <td>{{ e.patientName }}<div class="muted small">{{ e.patientAddress }}</div></td>
              <td>{{ e.prescriberName }} ({{ e.prescriberRegNo }})<div class="muted small">{{ e.prescriberAddress }}</div></td>
              <td>{{ e.drugName }}</td>
              <td>{{ e.batchNumber }}</td>
              <td>{{ e.quantity }}</td>
              <td>{{ e.pharmacistName }}</td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="muted">No Schedule {{ schedule }} sales in this period.</td></tr>
          }
        </tbody>
      </table>
      <p class="muted small">The Schedule H1 register must be kept for 3 years.</p>
    </div>
  `,
  styles: [`
    .head { display: flex; justify-content: space-between; align-items: center; }
    .toolbar { display: flex; gap: 1.25rem; align-items: center; margin-bottom: .75rem; flex-wrap: wrap; }
    .toolbar input[type=date] { padding: .4rem .5rem; border: 1px solid var(--border); border-radius: 6px; margin-left: .3rem; }
    td { font-size: .88rem; vertical-align: top; }
    .small { font-size: .78rem; }
  `]
})
export class RegisterComponent implements OnInit {
  schedule: 'H1' | 'X' = 'H1';
  from = localDate(-30);
  to = localDate();
  entries: RegisterEntry[] = [];
  error = '';

  constructor(private service: SaleService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.register(this.schedule, this.from, this.to).subscribe({
      next: list => { this.entries = list; this.error = ''; },
      error: () => this.error = 'Could not load the register.'
    });
  }

  print(): void {
    window.print();
  }
}
