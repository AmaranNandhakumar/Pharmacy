import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Medicine, MedicineDetail } from '../medicines/medicine.models';
import { MedicineService } from '../medicines/medicine.service';
import { PosComponent } from './pos.component';
import { BillableFill, Sale } from './sale.models';
import { SaleService } from './sale.service';

const fill = (fillId: number, patientId: number, total: number): BillableFill => ({
  fillId, prescriptionId: fillId * 10, patientId, patientName: `Patient ${patientId}`, prescriberName: 'Dr. Test',
  isRefill: false, dispensedAt: '2026-10-03T08:00:00Z', total, lines: [{ medicineName: 'Azinova', batchNumber: 'AZ1', quantity: 3, unitPrice: total / 3 }]
});

const paracip = { id: 5, name: 'Paracip', strength: '650 mg', schedule: 'G', sellableQuantity: 100 } as Medicine;

describe('PosComponent', () => {
  let fixture: ComponentFixture<PosComponent>;
  let component: PosComponent;
  let sales: jasmine.SpyObj<SaleService>;
  let router: Router;

  function setup(fillIdParam: string | null = null): void {
    sales = jasmine.createSpyObj<SaleService>('SaleService', ['billableFills', 'create']);
    sales.billableFills.and.returnValue(of([fill(1, 100, 90), fill(2, 100, 30), fill(3, 200, 50)]));
    const medicines = jasmine.createSpyObj<MedicineService>('MedicineService', ['search', 'get']);
    medicines.get.and.returnValue(of({ ...paracip, batches: [
      { id: 1, expiryDate: '2027-01-31', isExpired: false, quantityOnHand: 10, sellingPrice: 2 },
      { id: 2, expiryDate: '2026-12-31', isExpired: false, quantityOnHand: 10, sellingPrice: 1.9 }
    ] } as unknown as MedicineDetail));

    TestBed.configureTestingModule({
      imports: [PosComponent],
      providers: [
        provideRouter([]),
        { provide: SaleService, useValue: sales },
        { provide: MedicineService, useValue: medicines },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(fillIdParam ? { fillId: fillIdParam } : {}) } } }
      ]
    });
    fixture = TestBed.createComponent(PosComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  }

  it('preselects the prescription fill passed in the link', () => {
    setup('2');

    expect(component.chosenFills().map(f => f.fillId)).toEqual([2]);
  });

  it('prices a shelf item from the batch that expires first', () => {
    setup();

    component.add(paracip);

    expect(component.cart[0].estimatedPrice).toBe(1.9);
  });

  it('adds up fills and shelf items, less the discount', () => {
    setup();
    component.toggleFill(fill(1, 100, 90));
    component.add(paracip);
    component.cart[0].quantity = 10;   // 10 × 1.90 = 19

    component.discountPercent = 10;

    expect(component.estimatedTotal()).toBeCloseTo((90 + 19) * 0.9, 2);
  });

  it('keeps one patient per bill', () => {
    setup();

    component.toggleFill(fill(1, 100, 90));

    expect(component.selectedPatientId()).toBe(100);
    fixture.detectChanges();
    const boxes = fixture.nativeElement.querySelectorAll('.fill input[type=checkbox]') as NodeListOf<HTMLInputElement>;
    expect(Array.from(boxes).map(b => b.disabled)).toEqual([false, false, true]);
  });

  it('refuses a discount above the limit', () => {
    setup();
    component.discountPercent = 25;

    expect(component.validDiscount()).toBeFalse();
  });

  it('sends fills and shelf items, billed to the prescription patient, then opens the invoice', () => {
    setup();
    sales.create.and.returnValue(of({ id: 42 } as Sale));
    spyOn(router, 'navigate').and.resolveTo(true);
    component.toggleFill(fill(2, 100, 30));
    component.add(paracip);
    component.customerName = 'Ignored when billing a prescription';
    component.paymentMethod = 'Upi';

    component.checkout();

    expect(sales.create).toHaveBeenCalledWith({
      patientId: 100, customerName: null, paymentMethod: 'Upi', discountPercent: 0,
      items: [{ medicineId: 5, quantity: 1 }], prescriptionFillIds: [2]
    });
    expect(router.navigate).toHaveBeenCalledWith(['/sales', 42], { queryParams: { print: 1 } });
  });
});
