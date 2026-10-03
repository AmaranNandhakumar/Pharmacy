import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { PurchaseOrderDetailComponent } from './purchase-order-detail.component';
import { PurchaseOrder } from './purchasing.models';
import { PurchasingService } from './purchasing.service';

const po: PurchaseOrder = {
  id: 9, poNumber: 'PO/2026-27/0009', supplierId: 1, supplierName: 'Medline', status: 'Ordered', lineCount: 2,
  unitsOrdered: 60, unitsReceived: 10, estimatedValue: null, createdAt: '2026-10-01', orderedAt: '2026-10-02',
  notes: null, createdByName: 'Priya', completedAt: null,
  supplier: { id: 1, name: 'Medline', contactPerson: null, phone: null, email: null, address: null, gstin: null, drugLicenceNo: null, isActive: true, openOrders: 1 },
  lines: [
    { id: 11, medicineId: 1, medicineName: 'Cetrinova', strength: '10 mg', form: 'Tablet', packSize: null, quantityOrdered: 40, quantityReceived: 10, quantityOutstanding: 30, expectedRate: 0.9, sellableQuantity: 5, reorderLevel: 40 },
    { id: 12, medicineId: 2, medicineName: 'Electra ORS', strength: null, form: 'Powder', packSize: null, quantityOrdered: 20, quantityReceived: 0, quantityOutstanding: 20, expectedRate: null, sellableQuantity: 0, reorderLevel: 30 }
  ]
};

describe('PurchaseOrderDetailComponent receiving', () => {
  let fixture: ComponentFixture<PurchaseOrderDetailComponent>;
  let component: PurchaseOrderDetailComponent;
  let service: jasmine.SpyObj<PurchasingService>;

  beforeEach(() => {
    service = jasmine.createSpyObj<PurchasingService>('PurchasingService', ['order', 'receive', 'markOrdered', 'close']);
    service.order.and.returnValue(of(po));
    service.receive.and.returnValue(of(po));
    TestBed.configureTestingModule({
      imports: [PurchaseOrderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: PurchasingService, useValue: service },
        { provide: AuthService, useValue: { hasRole: () => true } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '9' }) } } }
      ]
    });
    fixture = TestBed.createComponent(PurchaseOrderDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function fillRow(i: number, values: Partial<(typeof component.rows)[number]>): void {
    Object.assign(component.rows[i], { batchNumber: 'B1', expiryMonth: '2027-02', mrp: 2, sellingPrice: 1.9, purchaseRate: 1, ...values });
  }

  it('prefills a row per outstanding line with the due quantity and expected rate', () => {
    expect(component.rows.map(r => [r.line.id, r.quantity, r.purchaseRate])).toEqual([[11, 30, 0.9], [12, 20, null]]);
  });

  it('asks for batch, expiry and prices on lines being received', () => {
    component.invoiceNo = 'INV-7';
    component.rows[1].quantity = 0;

    expect(component.receiveProblem()).toContain('Cetrinova');

    fillRow(0, {});
    expect(component.receiveProblem()).toBe('');
  });

  it('refuses a selling price above MRP', () => {
    component.invoiceNo = 'INV-7';
    component.rows[1].quantity = 0;
    fillRow(0, { mrp: 2, sellingPrice: 2.5 });

    expect(component.receiveProblem()).toContain('above its MRP');
  });

  it('counts split batches against what was ordered', () => {
    component.invoiceNo = 'INV-7';
    component.rows[1].quantity = 0;
    fillRow(0, { quantity: 20 });
    component.splitBatch(0);
    fillRow(1, { batchNumber: 'B2', quantity: 15 });   // 20 + 15 > 30 due

    expect(component.receiveProblem()).toContain('Only 30 more units');
  });

  it('sends the delivery with expiry as the last day of the month, skipping lines left at 0', () => {
    component.invoiceNo = ' INV-7 ';
    component.rows[1].quantity = 0;
    fillRow(0, { batchNumber: ' b1 ', expiryMonth: '2028-02', quantity: 30 });

    component.receive();

    expect(service.receive).toHaveBeenCalledWith(9, 'INV-7', [
      { lineId: 11, batchNumber: 'b1', expiryDate: '2028-02-29', mrp: 2, sellingPrice: 1.9, purchaseRate: 1, quantity: 30 }
    ]);
  });
});
