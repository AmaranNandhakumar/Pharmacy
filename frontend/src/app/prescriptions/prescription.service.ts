import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { DispenseResult, Prescription, PrescriptionRequest, PrescriptionStatus, PrescriptionSummary } from './prescription.models';

@Injectable({ providedIn: 'root' })
export class PrescriptionService {
  private url = `${environment.apiUrl}/prescriptions`;

  constructor(private http: HttpClient) {}

  list(status?: PrescriptionStatus | null, patientId?: number): Observable<PrescriptionSummary[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (patientId) params = params.set('patientId', patientId);
    return this.http.get<PrescriptionSummary[]>(this.url, { params });
  }

  get(id: number): Observable<Prescription> {
    return this.http.get<Prescription>(`${this.url}/${id}`);
  }

  create(req: PrescriptionRequest): Observable<Prescription> {
    return this.http.post<Prescription>(this.url, req);
  }

  verify(id: number, acknowledgeAllergyWarnings: boolean): Observable<Prescription> {
    return this.http.post<Prescription>(`${this.url}/${id}/verify`, { acknowledgeAllergyWarnings });
  }

  reject(id: number, reason: string): Observable<Prescription> {
    return this.http.post<Prescription>(`${this.url}/${id}/reject`, { reason });
  }

  dispense(id: number): Observable<DispenseResult> {
    return this.http.post<DispenseResult>(`${this.url}/${id}/dispense`, {});
  }
}
