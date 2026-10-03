import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Batch, Medicine, MedicineDetail, MedicineRequest, ReceiveStockRequest, StockAlerts } from './medicine.models';

@Injectable({ providedIn: 'root' })
export class MedicineService {
  private medicinesUrl = `${environment.apiUrl}/medicines`;
  private inventoryUrl = `${environment.apiUrl}/inventory`;

  constructor(private http: HttpClient) {}

  search(search: string, includeInactive = false): Observable<Medicine[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    if (includeInactive) params = params.set('includeInactive', true);
    return this.http.get<Medicine[]>(this.medicinesUrl, { params });
  }

  get(id: number): Observable<MedicineDetail> {
    return this.http.get<MedicineDetail>(`${this.medicinesUrl}/${id}`);
  }

  create(req: MedicineRequest): Observable<MedicineDetail> {
    return this.http.post<MedicineDetail>(this.medicinesUrl, req);
  }

  update(id: number, req: MedicineRequest): Observable<MedicineDetail> {
    return this.http.put<MedicineDetail>(`${this.medicinesUrl}/${id}`, req);
  }

  deactivate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.medicinesUrl}/${id}`);
  }

  receive(req: ReceiveStockRequest): Observable<Batch> {
    return this.http.post<Batch>(`${this.inventoryUrl}/receipts`, req);
  }

  adjust(batchId: number, quantityChange: number, reason: string): Observable<Batch> {
    return this.http.post<Batch>(`${this.inventoryUrl}/adjustments`, { batchId, quantityChange, reason });
  }

  alerts(expiringWithinDays = 90): Observable<StockAlerts> {
    return this.http.get<StockAlerts>(`${this.inventoryUrl}/alerts`, { params: { expiringWithinDays } });
  }
}
