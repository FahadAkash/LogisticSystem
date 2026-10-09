import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Check if token exists in storage to allow asynchronous session restore
  if (authService.getAccessToken()) {
    return true;
  }

  router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
  return false;
};

export const roleGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const expectedRoles = (route.data?.['roles'] as string[]) || [];
  if (expectedRoles.length === 0) {
    return true;
  }

  const userRoles = authService.roles();
  const hasRole = expectedRoles.some((r) => userRoles.includes(r));

  if (hasRole) {
    return true;
  }

  // Redirect to their default area
  if (userRoles.includes('Customer')) {
    router.navigate(['/customer']);
  } else if (userRoles.includes('Dispatcher')) {
    router.navigate(['/dispatcher']);
  } else if (userRoles.includes('Admin')) {
    router.navigate(['/admin']);
  } else {
    router.navigate(['/auth/login']);
  }
  return false;
};

