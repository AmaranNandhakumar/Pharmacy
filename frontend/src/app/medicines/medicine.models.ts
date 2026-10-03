export type DrugSchedule = 'Otc' | 'G' | 'H' | 'H1' | 'X' | 'Ndps';

export const DRUG_SCHEDULES: { value: DrugSchedule; label: string }[] = [
  { value: 'Otc', label: 'OTC' },
  { value: 'G', label: 'Schedule G' },
  { value: 'H', label: 'Schedule H' },
  { value: 'H1', label: 'Schedule H1' },
  { value: 'X', label: 'Schedule X' },
  { value: 'Ndps', label: 'NDPS' }
];

// Matches StockRules.AllowedGstRates on the API (12% slab removed in Sept 2025)
export const GST_RATES = [0, 5, 18];

export const DOSAGE_FORMS = ['Tablet', 'Capsule', 'Syrup', 'Suspension', 'Injection', 'Ointment', 'Cream', 'Drops', 'Inhaler', 'Powder', 'Other'];

export interface Medicine {
  id: number;
  name: string;
  genericName: string | null;
  strength: string | null;
  form: string;
  packSize: string | null;
  manufacturer: string | null;
  barcode: string | null;
  schedule: DrugSchedule;
  requiresPrescription: boolean;
  hsnCode: string;
  gstRatePercent: number;
  reorderLevel: number;
  isActive: boolean;
  sellableQuantity: number;
  nearestExpiry: string | null;
}

export interface MedicineDetail extends Medicine {
  batches: Batch[];
}

export interface MedicineRequest {
  name: string;
  genericName: string | null;
  strength: string | null;
  form: string;
  packSize: string | null;
  manufacturer: string | null;
  barcode: string | null;
  schedule: DrugSchedule;
  hsnCode: string;
  gstRatePercent: number;
  reorderLevel: number;
}

export interface Batch {
  id: number;
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  expiryDate: string;
  mrp: number;
  sellingPrice: number;
  purchaseRate: number;
  quantityOnHand: number;
  supplierName: string | null;
  supplierInvoiceNo: string | null;
  receivedAt: string;
  isExpired: boolean;
}

export interface ReceiveStockRequest {
  medicineId: number;
  batchNumber: string;
  expiryDate: string;
  mrp: number;
  sellingPrice: number;
  purchaseRate: number;
  quantity: number;
  supplierName: string | null;
  supplierInvoiceNo: string | null;
}

export interface LowStockItem {
  medicineId: number;
  name: string;
  strength: string | null;
  form: string;
  sellableQuantity: number;
  reorderLevel: number;
}

export interface StockAlerts {
  expiringWithinDays: number;
  lowStock: LowStockItem[];
  expiringSoon: Batch[];
  expired: Batch[];
}

export function scheduleLabel(s: DrugSchedule): string {
  return DRUG_SCHEDULES.find(x => x.value === s)?.label ?? s;
}
