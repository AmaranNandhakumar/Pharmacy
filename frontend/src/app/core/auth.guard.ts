import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { UserRole } from './models';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  if (authService.isLoggedIn()) return true;

  return inject(Router).createUrlTree(['/login']);
};

/** Hides routes a role can't use. The API still enforces every permission; this is convenience only. */
export const roleGuard = (...roles: UserRole[]): CanActivateFn => () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isLoggedIn()) return router.createUrlTree(['/login']);
  return authService.hasRole(...roles) ? true : router.createUrlTree(['/']);
};
