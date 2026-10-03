import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuditEntry, AuditPage } from './report.models';
import { AuditFilter, ReportService } from './report.service';

/** Who did what, and when. Read-only: the audit trail can't be edited or deleted from the app. */
@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h1>Audit log</h1>
    <div class="card">
      <div class="toolbar">
        <label>Action
          <select [(ngModel)]="filter.action" (change)="search()">
            <option value="">All</option>
            @for (a of actions; track a) { <option [value]="a">{{ a }}</option> }
          </select>
        </label>
        <label>Record type
          <select [(ngModel)]="filter.entityType" (change)="search()">
            <option value="">All</option>
            @for (t of entityTypes; track t) { <option [value]="t">{{ t }}</option> }
          </select>
        </label>
        <label>Record id <input [(ngModel)]="filter.entityId" (change)="search()" class="short"></label>
        <label>From <input type="date" [(ngModel)]="filter.from" (change)="search()"></label>
        <label>To <input type="date" [(ngModel)]="filter.to" (change)="search()"></label>
        <button class="btn-link" (click)="clear()">Clear</button>
      </div>
      @if (error) { <p class="error">{{ error }}</p> }
      <table>
        <thead><tr><th>When</th><th>Who</th><th>Action</th><th>Record</th><th>Details</th></tr></thead>
        <tbody>
          @for (e of page?.items ?? []; track e.id) {
            <tr>
              <td class="nowrap">{{ e.createdAt | date: 'd MMM yyyy, h:mm:ss a' }}</td>
              <td>{{ e.userName ?? 'System' }}</td>
              <td><span class="badge">{{ e.action }}</span></td>
              <td class="nowrap"><button class="btn-link" (click)="only(e)">{{ e.entityType }} #{{ e.entityId }}</button></td>
              <td class="details">{{ pretty(e.details) }}</td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="muted">No entries match.</td></tr>
          }
        </tbody>
      </table>
      @if (page && page.totalCount > page.pageSize) {
        <div class="pager">
          <button class="btn-link" (click)="go(page.page - 1)" [disabled]="page.page === 1">← Newer</button>
          <span class="muted">Page {{ page.page }} of {{ pages() }} · {{ page.totalCount }} entries</span>
          <button class="btn-link" (click)="go(page.page + 1)" [disabled]="page.page >= pages()">Older →</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toolbar { display: flex; gap: 1rem; align-items: center; margin-bottom: .75rem; flex-wrap: wrap; }
    .toolbar input, .toolbar select { padding: .4rem .5rem; border: 1px solid var(--border); border-radius: 6px; margin-left: .3rem; }
    .short { width: 6rem; }
    td { font-size: .88rem; vertical-align: top; }
    .nowrap { white-space: nowrap; }
    .details { font-family: ui-monospace, Consolas, monospace; font-size: .78rem; color: var(--muted); word-break: break-word; }
    .pager { display: flex; justify-content: space-between; align-items: center; margin-top: .75rem; }
  `]
})
export class AuditLogComponent implements OnInit {
  filter: AuditFilter = { action: '', entityType: '', entityId: '', from: '', to: '', page: 1, pageSize: 50 };
  page: AuditPage | null = null;
  actions: string[] = [];
  entityTypes: string[] = [];
  error = '';

  constructor(private service: ReportService) {}

  ngOnInit(): void {
    this.load();
  }

  search(): void {
    this.filter.page = 1;
    this.load();
  }

  go(page: number): void {
    this.filter.page = page;
    this.load();
  }

  only(e: AuditEntry): void {
    this.filter = { ...this.filter, entityType: e.entityType, entityId: e.entityId ?? '', action: '', page: 1 };
    this.load();
  }

  clear(): void {
    this.filter = { action: '', entityType: '', entityId: '', from: '', to: '', page: 1, pageSize: 50 };
    this.load();
  }

  pages(): number {
    return this.page ? Math.ceil(this.page.totalCount / this.page.pageSize) : 1;
  }

  /** Details are JSON; show them as "Key: value · Key: value". */
  pretty(details: string | null): string {
    if (!details) return '';
    try {
      const obj = JSON.parse(details) as Record<string, unknown>;
      return Object.entries(obj).map(([k, v]) => `${k}: ${typeof v === 'object' ? JSON.stringify(v) : v}`).join(' · ');
    } catch {
      return details;
    }
  }

  private load(): void {
    this.service.audit(this.filter).subscribe({
      next: p => {
        this.page = p;
        this.actions = p.actions;
        this.entityTypes = p.entityTypes;
        this.error = '';
      },
      error: err => this.error = err.error?.message || 'Could not load the audit log.'
    });
  }
}
