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
        path: 'users',
        loadComponent: () => import('./users/users.component').then(m => m.UsersComponent),
        canActivate: [roleGuard('Admin')]
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
