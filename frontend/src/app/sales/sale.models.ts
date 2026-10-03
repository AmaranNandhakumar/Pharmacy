import { DrugSchedule } from '../medicines/medicine.models';

export type PaymentMethod = 'Cash' | 'Card' | 'Upi';
export type SaleStatus = 'Completed' | 'Voided';

export const PAYMENT_METHODS: { value: PaymentMethod; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Upi', label: 'UPI' },
  { value: 'Card', label: 'Card' }
];

// Matches SaleRules.MaxDiscountPercent on the API
export const MAX_DISCOUNT_PERCENT = 20;

export interface SaleSummary {
  id: number;
  invoiceNo: string;
  createdAt: string;
  patientId: number | null;
  customerName: string | null;
  itemCount: number;
  total: number;
  paymentMethod: PaymentMethod;
  status: SaleStatus;
}

export interface SaleItem {
  id: number;
  medicineId: number;
  medicineName: string;
  prescriptionItemId: number | null;
  hsnCode: string;
  batchNumber: string;
  expiryDate: string;
  mrp: number;
  unitPrice: number;
  quantity: number;
  gstRatePercent: number;
  grossAmount: number;
  discount: number;
  taxableValue: number;
  cgst: number;
  sgst: number;
  lineTotal: number;
}

export interface GstSummary {
  gstRatePercent: number;
  taxableValue: number;
  cgst: number;
  sgst: number;
}

export interface PharmacySettings {
  name: string;
  address: string;
  phone: string | null;
  stateCode: string;
  gstin: string;
  drugLicence20: string;
  drugLicence21: string;
  registeredPharmacistName: string;
  registeredPharmacistRegNo: string;
}

export interface Sale extends SaleSummary {
  patientAddress: string | null;
  billedByName: string | null;
  discountPercent: number;
  grossAmount: number;
  discount: number;
  taxableValue: number;
  cgst: number;
  sgst: number;
  voidedByName: string | null;
  voidedAt: string | null;
  voidReason: string | null;
  items: SaleItem[];
  gstSummary: GstSummary[];
  pharmacy: PharmacySettings;
}

export interface CreateSaleRequest {
  patientId: number | null;
  customerName: string | null;
  paymentMethod: PaymentMethod;
  discountPercent: number;
  items: { medicineId: number; quantity: number }[];
  prescriptionFillIds: number[];
}

export interface BillableFill {
  fillId: number;
  prescriptionId: number;
  patientId: number;
  patientName: string;
  prescriberName: string;
  isRefill: boolean;
  dispensedAt: string;
  total: number;
  lines: { medicineName: string; batchNumber: string; quantity: number; unitPrice: number }[];
}

export interface RegisterEntry {
  id: number;
  schedule: DrugSchedule;
  createdAt: string;
  invoiceNo: string;
  patientName: string;
  patientAddress: string | null;
  prescriberName: string;
  prescriberRegNo: string;
  prescriberAddress: string | null;
  drugName: string;
  batchNumber: string;
  quantity: number;
  pharmacistName: string | null;
}
