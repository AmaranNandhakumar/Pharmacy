import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const isAuthCall = (url: string) => /\/auth\/(login|refresh|logout)$/.test(url);

/**
 * Adds the access token. If it has run out, refreshes first; if the API still answers 401
 * (revoked or expired server side), refreshes once and retries. When refreshing fails the
 * session is over: back to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (isAuthCall(req.url)) return next(req);

  const withToken = (r: HttpRequest<unknown>) => {
    const token = auth.getToken();
    return token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };
  const endSession = (err: unknown) => {
    auth.clear();
    router.navigate(['/login']);
    return throwError(() => err);
  };

  // Access token expired but the session is still valid: renew before sending
  const ready$ = !auth.getToken() && auth.canRefresh() ? auth.refresh() : of(true);

  return ready$.pipe(
    switchMap(() => next(withToken(req))),
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || !auth.canRefresh()) {
        return err.status === 401 && auth.currentUser() ? endSession(err) : throwError(() => err);
      }
      return auth.refresh().pipe(
        switchMap(ok => ok ? next(withToken(req)) : endSession(err))
      );
    })
  );
};
