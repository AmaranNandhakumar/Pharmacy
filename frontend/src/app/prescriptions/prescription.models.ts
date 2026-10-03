import { DrugSchedule } from '../medicines/medicine.models';

export type PrescriptionStatus = 'Entered' | 'Verified' | 'Dispensed' | 'Rejected';

export const PRESCRIPTION_STATUSES: PrescriptionStatus[] = ['Entered', 'Verified', 'Dispensed', 'Rejected'];

export interface PrescriptionSummary {
  id: number;
  patientId: number;
  patientName: string;
  prescriberName: string;
  issuedOn: string;
  status: PrescriptionStatus;
  itemCount: number;
  createdAt: string;
}

export interface PrescriptionItem {
  id: number;
  medicineId: number;
  medicineName: string;
  genericName: string | null;
  strength: string | null;
  schedule: DrugSchedule;
  dose: string;
  quantity: number;
  directions: string;
  refillsAllowed: number;
  refillsUsed: number;
  quantityDispensed: number;
  sellableQuantity: number;
  allergyWarnings: string[];
}

export interface Prescription extends PrescriptionSummary {
  patientAllergies: string | null;
  prescriberRegNo: string;
  prescriberAddress: string | null;
  copyRetained: boolean;
  rejectReason: string | null;
  enteredByName: string | null;
  verifiedByName: string | null;
  verifiedAt: string | null;
  dispensedByName: string | null;
  dispensedAt: string | null;
  hasAllergyWarnings: boolean;
  items: PrescriptionItem[];
}

export interface PrescriptionItemRequest {
  medicineId: number;
  dose: string;
  quantity: number;
  directions: string;
  refillsAllowed: number;
}

export interface PrescriptionRequest {
  patientId: number;
  prescriberName: string;
  prescriberRegNo: string;
  prescriberAddress: string | null;
  issuedOn: string;
  copyRetained: boolean;
  items: PrescriptionItemRequest[];
}

export interface DispensedLine {
  prescriptionItemId: number;
  medicineName: string;
  batchId: number;
  batchNumber: string;
  expiryDate: string;
  mrp: number;
  quantity: number;
}

export interface DispenseResult {
  prescription: Prescription;
  wasRefill: boolean;
  lines: DispensedLine[];
}
