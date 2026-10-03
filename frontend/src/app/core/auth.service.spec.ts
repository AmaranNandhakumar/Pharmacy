import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { authResponse } from './test-helpers';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  const api = environment.apiUrl;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function login(res = authResponse()): void {
    service.login('priya@pharmacy.test', 'secret').subscribe();
    http.expectOne(`${api}/auth/login`).flush(res);
  }

  it('stores the session and knows the role after login', () => {
    login();

    expect(service.isLoggedIn()).toBeTrue();
    expect(service.getToken()).toBe('access-1');
    expect(service.hasRole('Pharmacist')).toBeTrue();
    expect(service.hasRole('Admin')).toBeFalse();
    expect(sessionStorage.getItem('pharmacy_session')).toContain('refresh-1');
  });

  it('stays logged in when only the access token has expired', () => {
    login(authResponse({ expiresAt: new Date(Date.now() - 1000).toISOString() }));

    expect(service.getToken()).toBeNull();
    expect(service.isLoggedIn()).toBeTrue();
    expect(service.canRefresh()).toBeTrue();
  });

  it('is logged out once the refresh token has expired too', () => {
    const past = new Date(Date.now() - 1000).toISOString();
    login(authResponse({ expiresAt: past, refreshExpiresAt: past }));

    expect(service.isLoggedIn()).toBeFalse();
  });

  it('sends one refresh request for concurrent callers and stores the new pair', () => {
    login();
    const results: boolean[] = [];

    service.refresh().subscribe(ok => results.push(ok));
    service.refresh().subscribe(ok => results.push(ok));
    const req = http.expectOne(`${api}/auth/refresh`);
    expect(req.request.body).toEqual({ refreshToken: 'refresh-1' });
    req.flush(authResponse({ token: 'access-2', refreshToken: 'refresh-2' }));

    expect(results).toEqual([true, true]);
    expect(service.getToken()).toBe('access-2');
  });

  it('clears the session when the refresh is refused', () => {
    login();
    let result: boolean | undefined;

    service.refresh().subscribe(ok => result = ok);
    http.expectOne(`${api}/auth/refresh`).flush({ message: 'Invalid' }, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeFalse();
    expect(service.isLoggedIn()).toBeFalse();
    expect(service.currentUser()).toBeNull();
  });

  it('revokes the refresh token on the server when logging out', () => {
    login();

    service.logout();

    expect(http.expectOne(`${api}/auth/logout`).request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(service.isLoggedIn()).toBeFalse();
  });
});
