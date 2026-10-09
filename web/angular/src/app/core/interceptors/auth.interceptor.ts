import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const correlationId = 'web-' + Math.random().toString(36).substring(2, 10);
  const token = authService.getAccessToken();

  let modifiedReq = req.clone({
    setHeaders: {
      'X-Correlation-Id': correlationId,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  return next(modifiedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('/api/auth/login')) {
        authService.logout();
        router.navigate(['/auth/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    })
  );
};
