import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuditPage, DailySalesReport, ExpiringReport, GstSummaryReport, StockValuationReport } from './report.models';

export interface AuditFilter {
  entityType?: string;
  entityId?: string;
  action?: string;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class ReportService {
  private url = `${environment.apiUrl}/reports`;
  private auditUrl = `${environment.apiUrl}/audit`;

  constructor(private http: HttpClient) {}

  dailySales(from: string, to: string): Observable<DailySalesReport> {
    return this.http.get<DailySalesReport>(`${this.url}/daily-sales`, { params: { from, to } });
  }

  gstSummary(from: string, to: string): Observable<GstSummaryReport> {
    return this.http.get<GstSummaryReport>(`${this.url}/gst-summary`, { params: { from, to } });
  }

  stockValuation(): Observable<StockValuationReport> {
    return this.http.get<StockValuationReport>(`${this.url}/stock-valuation`);
  }

  expiring(withinDays: number): Observable<ExpiringReport> {
    return this.http.get<ExpiringReport>(`${this.url}/expiring`, { params: { withinDays } });
  }

  audit(filter: AuditFilter): Observable<AuditPage> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, value);
    }
    return this.http.get<AuditPage>(this.auditUrl, { params });
  }
}

/** Rows as CSV text: fields with commas, quotes or line breaks are quoted and quotes doubled (RFC 4180). */
export function toCsv(header: string[], rows: (string | number | null)[][]): string {
  const escape = (v: string | number | null) => {
    const s = v === null ? '' : String(v);
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  return [header, ...rows].map(r => r.map(escape).join(',')).join('\r\n');
}

/** Downloads rows as a CSV file that opens in Excel (UTF-8 with BOM so ₹ and names survive). */
export function downloadCsv(filename: string, header: string[], rows: (string | number | null)[][]): void {
  const csv = toCsv(header, rows);
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = filename;
  link.click();
  URL.revokeObjectURL(link.href);
}
