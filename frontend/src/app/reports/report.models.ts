import { DrugSchedule } from '../medicines/medicine.models';

export interface MoneyTotals {
  gross: number;
  discount: number;
  taxableValue: number;
  cgst: number;
  sgst: number;
  total: number;
}

export interface DailySalesRow extends MoneyTotals {
  date: string;
  bills: number;
  voidedBills: number;
  cash: number;
  upi: number;
  card: number;
}

export interface DailySalesReport {
  from: string;
  to: string;
  days: DailySalesRow[];
  totals: DailySalesRow;
}

export interface GstRateRow {
  gstRatePercent: number;
  taxableValue: number;
  cgst: number;
  sgst: number;
  total: number;
}

export interface HsnRow extends GstRateRow {
  hsnCode: string;
  quantity: number;
}

export interface GstSummaryReport {
  from: string;
  to: string;
  bills: number;
  voidedBills: number;
  firstInvoiceNo: string;
  lastInvoiceNo: string;
  byRate: GstRateRow[];
  byHsn: HsnRow[];
  totals: GstRateRow;
}

export interface StockValuationRow {
  medicineId: number;
  name: string;
  strength: string | null;
  schedule: DrugSchedule;
  quantity: number;
  purchaseValue: number;
  salesValue: number;
  expiredQuantity: number;
  expiredPurchaseValue: number;
}

export interface StockValuationReport {
  asOf: string;
  rows: StockValuationRow[];
  quantity: number;
  purchaseValue: number;
  salesValue: number;
  expiredQuantity: number;
  expiredPurchaseValue: number;
}

export interface ExpiringRow {
  batchId: number;
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  expiryDate: string;
  daysLeft: number;
  quantity: number;
  purchaseValue: number;
  supplierName: string | null;
}

export interface ExpiringReport {
  asOf: string;
  withinDays: number;
  rows: ExpiringRow[];
  purchaseValueAtRisk: number;
}

export interface AuditEntry {
  id: number;
  createdAt: string;
  userId: number | null;
  userName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  details: string | null;
}

export interface AuditPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: AuditEntry[];
  actions: string[];
  entityTypes: string[];
}
