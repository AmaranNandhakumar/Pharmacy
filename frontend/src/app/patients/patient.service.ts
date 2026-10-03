import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Patient, PatientDetail, PatientRequest } from './patient.models';

@Injectable({ providedIn: 'root' })
export class PatientService {
  private url = `${environment.apiUrl}/patients`;

  constructor(private http: HttpClient) {}

  search(search: string): Observable<Patient[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<Patient[]>(this.url, { params });
  }

  /** Opening a patient record is audited on the API. */
  get(id: number): Observable<PatientDetail> {
    return this.http.get<PatientDetail>(`${this.url}/${id}`);
  }

  create(req: PatientRequest): Observable<PatientDetail> {
    return this.http.post<PatientDetail>(this.url, req);
  }

  update(id: number, req: PatientRequest): Observable<PatientDetail> {
    return this.http.put<PatientDetail>(`${this.url}/${id}`, req);
  }
}
