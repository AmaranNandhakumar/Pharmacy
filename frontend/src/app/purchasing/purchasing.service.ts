import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  PurchaseOrder, PurchaseOrderRequest, PurchaseOrderStatus, PurchaseOrderSummary, ReceiveLineRequest,
  ReorderSuggestion, Supplier, SupplierRequest
} from './purchasing.models';

@Injectable({ providedIn: 'root' })
export class PurchasingService {
  private suppliersUrl = `${environment.apiUrl}/suppliers`;
  private poUrl = `${environment.apiUrl}/purchase-orders`;

  constructor(private http: HttpClient) {}

  suppliers(includeInactive = false): Observable<Supplier[]> {
    return this.http.get<Supplier[]>(this.suppliersUrl, { params: { includeInactive } });
  }

  createSupplier(req: SupplierRequest): Observable<Supplier> {
    return this.http.post<Supplier>(this.suppliersUrl, req);
  }

  updateSupplier(id: number, req: SupplierRequest): Observable<Supplier> {
    return this.http.put<Supplier>(`${this.suppliersUrl}/${id}`, req);
  }

  deactivateSupplier(id: number): Observable<void> {
    return this.http.delete<void>(`${this.suppliersUrl}/${id}`);
  }

  orders(status?: PurchaseOrderStatus | null): Observable<PurchaseOrderSummary[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<PurchaseOrderSummary[]>(this.poUrl, { params });
  }

  order(id: number): Observable<PurchaseOrder> {
    return this.http.get<PurchaseOrder>(`${this.poUrl}/${id}`);
  }

  suggestions(): Observable<ReorderSuggestion[]> {
    return this.http.get<ReorderSuggestion[]>(`${this.poUrl}/suggestions`);
  }

  create(req: PurchaseOrderRequest): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(this.poUrl, req);
  }

  update(id: number, req: PurchaseOrderRequest): Observable<PurchaseOrder> {
    return this.http.put<PurchaseOrder>(`${this.poUrl}/${id}`, req);
  }

  markOrdered(id: number): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`${this.poUrl}/${id}/order`, {});
  }

  close(id: number, reason: string | null): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`${this.poUrl}/${id}/close`, { reason });
  }

  receive(id: number, supplierInvoiceNo: string, lines: ReceiveLineRequest[]): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`${this.poUrl}/${id}/receive`, { supplierInvoiceNo, lines });
  }
}
