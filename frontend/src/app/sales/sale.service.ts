import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { BillableFill, CreateSaleRequest, PharmacySettings, RegisterEntry, Sale, SaleSummary } from './sale.models';

@Injectable({ providedIn: 'root' })
export class SaleService {
  private url = `${environment.apiUrl}/sales`;
  private settingsUrl = `${environment.apiUrl}/settings`;

  constructor(private http: HttpClient) {}

  create(req: CreateSaleRequest): Observable<Sale> {
    return this.http.post<Sale>(this.url, req);
  }

  /** Dates are yyyy-MM-dd in the pharmacy's local time; both days are included. */
  list(from: string, to: string): Observable<SaleSummary[]> {
    return this.http.get<SaleSummary[]>(this.url, { params: { from, to } });
  }

  get(id: number): Observable<Sale> {
    return this.http.get<Sale>(`${this.url}/${id}`);
  }

  void(id: number, reason: string): Observable<Sale> {
    return this.http.post<Sale>(`${this.url}/${id}/void`, { reason });
  }

  billableFills(patientId?: number): Observable<BillableFill[]> {
    let params = new HttpParams();
    if (patientId) params = params.set('patientId', patientId);
    return this.http.get<BillableFill[]>(`${this.url}/billable-fills`, { params });
  }

  register(schedule: 'H1' | 'X', from: string, to: string): Observable<RegisterEntry[]> {
    return this.http.get<RegisterEntry[]>(`${this.url}/register`, { params: { schedule, from, to } });
  }

  settings(): Observable<PharmacySettings> {
    return this.http.get<PharmacySettings>(this.settingsUrl);
  }

  updateSettings(s: PharmacySettings): Observable<PharmacySettings> {
    return this.http.put<PharmacySettings>(this.settingsUrl, s);
  }
}

/** Today's date as yyyy-MM-dd in local time (toISOString would give the UTC date). */
export function localDate(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
