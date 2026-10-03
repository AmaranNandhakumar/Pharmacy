import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, User, UserRole } from './models';

const SESSION_KEY = 'pharmacy_session';

interface Session {
  token: string;
  expiresAt: string;
  user: User;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  // sessionStorage, not localStorage: the session ends when the browser tab closes
  private session = signal<Session | null>(this.readSession());

  readonly currentUser = computed(() => this.session()?.user ?? null);

  constructor(private http: HttpClient) {}

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap(res => this.setSession(res)));
  }

  logout(): void {
    sessionStorage.removeItem(SESSION_KEY);
    this.session.set(null);
  }

  getToken(): string | null {
    return this.isLoggedIn() ? this.session()!.token : null;
  }

  isLoggedIn(): boolean {
    const s = this.session();
    return !!s && new Date(s.expiresAt).getTime() > Date.now();
  }

  hasRole(...roles: UserRole[]): boolean {
    const user = this.currentUser();
    return !!user && roles.includes(user.role);
  }

  private setSession(res: AuthResponse): void {
    const s: Session = { token: res.token, expiresAt: res.expiresAt, user: res.user };
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
