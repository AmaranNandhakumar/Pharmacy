export type PurchaseOrderStatus = 'Draft' | 'Ordered' | 'PartiallyReceived' | 'Received' | 'Closed' | 'Cancelled';

export const PO_STATUSES: { value: PurchaseOrderStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Ordered', label: 'Ordered' },
  { value: 'PartiallyReceived', label: 'Part received' },
  { value: 'Received', label: 'Received' },
  { value: 'Closed', label: 'Closed short' },
  { value: 'Cancelled', label: 'Cancelled' }
];

export function poStatusLabel(s: PurchaseOrderStatus): string {
  return PO_STATUSES.find(x => x.value === s)?.label ?? s;
}

export interface Supplier {
  id: number;
  name: string;
  contactPerson: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  gstin: string | null;
  drugLicenceNo: string | null;
  isActive: boolean;
  openOrders: number;
}

export type SupplierRequest = Omit<Supplier, 'id' | 'isActive' | 'openOrders'>;

export interface PurchaseOrderSummary {
  id: number;
  poNumber: string;
  supplierId: number;
  supplierName: string;
  status: PurchaseOrderStatus;
  lineCount: number;
  unitsOrdered: number;
  unitsReceived: number;
  estimatedValue: number | null;
  createdAt: string;
  orderedAt: string | null;
}

export interface PurchaseOrderLine {
  id: number;
  medicineId: number;
  medicineName: string;
  strength: string | null;
  form: string;
  packSize: string | null;
  quantityOrdered: number;
  quantityReceived: number;
  quantityOutstanding: number;
  expectedRate: number | null;
  sellableQuantity: number;
  reorderLevel: number;
}

export interface PurchaseOrder extends PurchaseOrderSummary {
  notes: string | null;
  createdByName: string | null;
  completedAt: string | null;
  supplier: Supplier;
  lines: PurchaseOrderLine[];
}

export interface PurchaseOrderRequest {
  supplierId: number;
  notes: string | null;
  lines: { medicineId: number; quantity: number; expectedRate: number | null }[];
}

export interface ReceiveLineRequest {
  lineId: number;
  batchNumber: string;
  expiryDate: string;
  mrp: number;
  sellingPrice: number;
  purchaseRate: number;
  quantity: number;
}

export interface ReorderSuggestion {
  medicineId: number;
  name: string;
  strength: string | null;
  form: string;
  sellableQuantity: number;
  reorderLevel: number;
  onOrder: number;
  suggestedQuantity: number;
  lastSupplierId: number | null;
  lastSupplierName: string | null;
  lastPurchaseRate: number | null;
}
