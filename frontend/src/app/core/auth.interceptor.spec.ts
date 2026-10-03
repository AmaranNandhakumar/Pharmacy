import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { authResponse } from './test-helpers';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;
  let router: Router;
  const api = environment.apiUrl;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  afterEach(() => backend.verify());

  function signIn(overrides = {}): void {
    auth.login('priya@pharmacy.test', 'secret').subscribe();
    backend.expectOne(`${api}/auth/login`).flush(authResponse(overrides));
  }

  it('adds the bearer token', () => {
    signIn();

    http.get(`${api}/medicines`).subscribe();

    expect(backend.expectOne(`${api}/medicines`).request.headers.get('Authorization')).toBe('Bearer access-1');
  });

  it('refreshes first when the access token has already expired', () => {
    signIn({ expiresAt: new Date(Date.now() - 1000).toISOString() });

    http.get(`${api}/medicines`).subscribe();
    backend.expectOne(`${api}/auth/refresh`).flush(authResponse({ token: 'access-2', refreshToken: 'refresh-2' }));

    expect(backend.expectOne(`${api}/medicines`).request.headers.get('Authorization')).toBe('Bearer access-2');
  });

  it('on a 401, refreshes once and retries with the new token', () => {
    signIn();
    let body: unknown;

    http.get(`${api}/patients`).subscribe(b => body = b);
    backend.expectOne(`${api}/patients`).flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${api}/auth/refresh`).flush(authResponse({ token: 'access-2', refreshToken: 'refresh-2' }));
    const retry = backend.expectOne(`${api}/patients`);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush([{ id: 1 }]);

    expect(body).toEqual([{ id: 1 }]);
  });

  it('sends the user to login when the refresh is refused', () => {
    signIn();
    let failed = false;

    http.get(`${api}/patients`).subscribe({ error: () => failed = true });
    backend.expectOne(`${api}/patients`).flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${api}/auth/refresh`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBeTrue();
    expect(auth.isLoggedIn()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('passes other errors straight through', () => {
    signIn();
    let status = 0;

    http.get(`${api}/sales/99`).subscribe({ error: e => status = e.status });
    backend.expectOne(`${api}/sales/99`).flush(null, { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
    expect(auth.isLoggedIn()).toBeTrue();
  });
});
