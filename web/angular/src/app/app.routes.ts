import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'customer',
  },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'auth/register',
    loadComponent: () =>
      import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'customer',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Customer', 'Dispatcher', 'Admin'] },
    loadComponent: () =>
      import('./features/customer/customer-portal.component').then(
        (m) => m.CustomerPortalComponent
      ),
  },
  {
    path: 'dispatcher',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Dispatcher', 'Admin'] },
    loadComponent: () =>
      import('./features/dispatcher/dispatcher-console.component').then(
        (m) => m.DispatcherConsoleComponent
      ),
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/admin/admin-panel.component').then((m) => m.AdminPanelComponent),
  },
  {
    path: '**',
    redirectTo: 'customer',
  },
];
