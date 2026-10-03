import { Injectable, computed, signal } from '@angular/core';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, User, UserRole } from './models';

const SESSION_KEY = 'pharmacy_session';

interface Session {
  token: string;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  user: User;
}

/**
 * Holds the signed-in session. The access token is short-lived (15 minutes); the refresh token
 * (single use, days) quietly gets a new one, so staff aren't logged out mid-shift.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  // sessionStorage, not localStorage: the session ends when the browser tab closes
  private session = signal<Session | null>(this.readSession());
  private refreshing$: Observable<boolean> | null = null;

  /** Calls made without the auth interceptor, so refreshing can't loop back into itself. */
  private plainHttp: HttpClient;

  readonly currentUser = computed(() => this.session()?.user ?? null);

  constructor(private http: HttpClient, backend: HttpBackend) {
    this.plainHttp = new HttpClient(backend);
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap(res => this.setSession(res)));
  }

  /** Ends this session on the server too (the refresh token stops working), then locally. */
  logout(): void {
    const s = this.session();
    if (s?.refreshToken) {
      this.plainHttp.post(`${environment.apiUrl}/auth/logout`, { refreshToken: s.refreshToken }).subscribe({ error: () => {} });
    }
    this.clear();
  }

  /** Forgets the session without telling the server (used when the server already refused it). */
  clear(): void {
    sessionStorage.removeItem(SESSION_KEY);
    this.session.set(null);
  }

  changePassword(currentPassword: string, newPassword: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/change-password`, { currentPassword, newPassword })
      .pipe(tap(res => this.setSession(res)));
  }

  logoutEverywhere(): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/logout-all`, {}).pipe(finalize(() => this.clear()));
  }

  /** The access token, or null when it has expired (the interceptor then refreshes first). */
  getToken(): string | null {
    const s = this.session();
    return s && !this.isExpired(s.expiresAt) ? s.token : null;
  }

  /** Signed in while the refresh token is still valid, even if the access token needs renewing. */
  isLoggedIn(): boolean {
    const s = this.session();
    return !!s && !this.isExpired(s.refreshExpiresAt ?? s.expiresAt);
  }

  canRefresh(): boolean {
    const s = this.session();
    return !!s?.refreshToken && !this.isExpired(s.refreshExpiresAt);
  }

  hasRole(...roles: UserRole[]): boolean {
    const user = this.currentUser();
    return !!user && roles.includes(user.role);
  }

  /**
   * Swaps the refresh token for a new pair. Concurrent callers share one request, because each
   * refresh token works only once. Emits true on success; on failure the session is cleared.
   */
  refresh(): Observable<boolean> {
    const s = this.session();
    if (!s?.refreshToken) return of(false);

    this.refreshing$ ??= this.plainHttp.post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken: s.refreshToken }).pipe(
      tap(res => this.setSession(res)),
      map(() => true),
      catchError(() => { this.clear(); return of(false); }),
      finalize(() => this.refreshing$ = null),
      shareReplay(1)
    );
    return this.refreshing$;
  }

  /** Treat a token as expired 30 seconds early, so it doesn't run out in flight. */
  private isExpired(iso: string | undefined): boolean {
    return !iso || new Date(iso).getTime() - 30_000 <= Date.now();
  }

  private setSession(res: AuthResponse): void {
    const s: Session = {
      token: res.token, expiresAt: res.expiresAt, refreshToken: res.refreshToken,
      refreshExpiresAt: res.refreshExpiresAt, user: res.user
    };
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(s));
    this.session.set(s);
  }

  private readSession(): Session | null {
    try {
      const raw = sessionStorage.getItem(SESSION_KEY);
      return raw ? JSON.parse(raw) as Session : null;
    } catch {
      return null;
    }
  }
}
