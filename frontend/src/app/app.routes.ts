import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth.guard';
import { ShellComponent } from './core/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'medicines',
        loadComponent: () => import('./medicines/medicine-list.component').then(m => m.MedicineListComponent)
      },
      {
        path: 'medicines/:id',
        loadComponent: () => import('./medicines/medicine-detail.component').then(m => m.MedicineDetailComponent)
      },
      {
        path: 'stock-alerts',
        loadComponent: () => import('./inventory/stock-alerts.component').then(m => m.StockAlertsComponent)
      },
      {
        path: 'patients',
        loadComponent: () => import('./patients/patient-list.component').then(m => m.PatientListComponent)
      },
      {
        path: 'patients/:id',
        loadComponent: () => import('./patients/patient-detail.component').then(m => m.PatientDetailComponent)
      },
      {
        path: 'prescriptions',
        loadComponent: () => import('./prescriptions/prescription-list.component').then(m => m.PrescriptionListComponent)
      },
      {
        path: 'prescriptions/new',
        loadComponent: () => import('./prescriptions/prescription-form.component').then(m => m.PrescriptionFormComponent)
      },
      {
        path: 'prescriptions/:id',
        loadComponent: () => import('./prescriptions/prescription-detail.component').then(m => m.PrescriptionDetailComponent)
      },
      {
        path: 'pos',
        loadComponent: () => import('./sales/pos.component').then(m => m.PosComponent)
      },
      {
        path: 'sales',
        loadComponent: () => import('./sales/sales-list.component').then(m => m.SalesListComponent)
      },
      {
        path: 'sales/:id',
        loadComponent: () => import('./sales/invoice.component').then(m => m.InvoiceComponent)
      },
      {
        path: 'register',
        loadComponent: () => import('./sales/register.component').then(m => m.RegisterComponent),
        canActivate: [roleGuard('Admin', 'Pharmacist')]
      },
      {
        path: 'suppliers',
        loadComponent: () => import('./purchasing/suppliers.component').then(m => m.SuppliersComponent),
        canActivate: [roleGuard('Admin', 'Pharmacist')]
      },
      {
        path: 'purchase-orders',
        loadComponent: () => import('./purchasing/purchase-order-list.component').then(m => m.PurchaseOrderListComponent)
      },
      {
        path: 'purchase-orders/new',
        loadComponent: () => import('./purchasing/purchase-order-form.component').then(m => m.PurchaseOrderFormComponent),
        canActivate: [roleGuard('Admin', 'Pharmacist')]
      },
      {
        path: 'purchase-orders/:id/edit',
        loadComponent: () => import('./purchasing/purchase-order-form.component').then(m => m.PurchaseOrderFormComponent),
        canActivate: [roleGuard('Admin', 'Pharmacist')]
      },
      {
        path: 'purchase-orders/:id',
        loadComponent: () => import('./purchasing/purchase-order-detail.component').then(m => m.PurchaseOrderDetailComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./reports/reports.component').then(m => m.ReportsComponent),
        canActivate: [roleGuard('Admin', 'Pharmacist')]
      },
      {
        path: 'audit',
        loadComponent: () => import('./reports/audit-log.component').then(m => m.AuditLogComponent),
        canActivate: [roleGuard('Admin')]
      },
      {
        path: 'settings',
        loadComponent: () => import('./sales/settings.component').then(m => m.SettingsComponent),
        canActivate: [roleGuard('Admin')]
      },
      {
        path: 'users',
        loadComponent: () => import('./users/users.component').then(m => m.UsersComponent),
        canActivate: [roleGuard('Admin')]
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
