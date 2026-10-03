import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { PrescriptionDetailComponent } from './prescription-detail.component';
import { Prescription } from './prescription.models';
import { PrescriptionService } from './prescription.service';

function rx(overrides: Partial<Prescription> = {}): Prescription {
  return {
    id: 8, patientId: 3, patientName: 'Fatima Begum', prescriberName: 'Dr. K. Anand', issuedOn: '2026-10-03', status: 'Entered',
    itemCount: 1, createdAt: '2026-10-03T05:00:00Z', patientAllergies: 'Amoxicillin', prescriberRegNo: 'TNMC 11223',
    prescriberAddress: null, copyRetained: false, rejectReason: null, enteredByName: 'Karthik', verifiedByName: null, verifiedAt: null,
    dispensedByName: null, dispensedAt: null, hasAllergyWarnings: true,
    items: [{ id: 1, medicineId: 4, medicineName: 'Amoxinate', genericName: 'Amoxicillin', strength: '500 mg', schedule: 'H', dose: '1 capsule',
      quantity: 15, directions: 'Three times a day', refillsAllowed: 0, refillsUsed: 0, quantityDispensed: 0, sellableQuantity: 100,
      allergyWarnings: ['Amoxicillin'] }],
    ...overrides
  };
}

describe('PrescriptionDetailComponent', () => {
  let fixture: ComponentFixture<PrescriptionDetailComponent>;
  let service: jasmine.SpyObj<PrescriptionService>;

  function setup(role: string, prescription = rx()): HTMLElement {
    service = jasmine.createSpyObj<PrescriptionService>('PrescriptionService', ['get', 'verify', 'reject', 'dispense']);
    service.get.and.returnValue(of(prescription));
    service.verify.and.returnValue(of(rx({ status: 'Verified' })));
    TestBed.configureTestingModule({
      imports: [PrescriptionDetailComponent],
      providers: [
        provideRouter([]),
        { provide: PrescriptionService, useValue: service },
        { provide: AuthService, useValue: { hasRole: (...roles: string[]) => roles.includes(role) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '8' }) } } }
      ]
    });
    fixture = TestBed.createComponent(PrescriptionDetailComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const button = (el: HTMLElement, text: string) =>
    Array.from(el.querySelectorAll('button')).find(b => b.textContent?.trim() === text) as HTMLButtonElement | undefined;

  it('shows the allergy warning', () => {
    const el = setup('Pharmacist');

    expect(el.querySelector('.warning')?.textContent).toContain('Amoxinate matches "Amoxicillin"');
  });

  it('keeps Verify disabled until the pharmacist acknowledges the warning', () => {
    const el = setup('Pharmacist');
    const verify = button(el, 'Verify')!;
    expect(verify.disabled).toBeTrue();

    const ack = el.querySelector('.check input') as HTMLInputElement;
    ack.click();
    fixture.detectChanges();
    expect(verify.disabled).toBeFalse();

    verify.click();
    expect(service.verify).toHaveBeenCalledWith(8, true);
  });

  it('offers no verify or dispense actions to a technician', () => {
    const el = setup('Technician');

    expect(button(el, 'Verify')).toBeUndefined();
    expect(el.textContent).toContain('Waiting for a pharmacist to verify.');
  });

  it('offers a refill only while refills are left', () => {
    const dispensed = rx({ status: 'Dispensed', hasAllergyWarnings: false });
    dispensed.items[0] = { ...dispensed.items[0], refillsAllowed: 1, refillsUsed: 1, allergyWarnings: [] };

    const el = setup('Pharmacist', dispensed);

    expect(button(el, 'Dispense refill')).toBeUndefined();
  });
});
