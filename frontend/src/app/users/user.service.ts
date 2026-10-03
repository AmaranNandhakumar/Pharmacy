import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { User, UserRole } from '../core/models';

export interface CreateUserRequest {
  email: string;
  fullName: string;
  password: string;
  role: UserRole;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private apiUrl = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<User[]> {
    return this.http.get<User[]>(this.apiUrl);
  }

  create(req: CreateUserRequest): Observable<User> {
    return this.http.post<User>(this.apiUrl, req);
  }

  update(id: number, fullName: string, role: UserRole): Observable<User> {
    return this.http.put<User>(`${this.apiUrl}/${id}`, { fullName, role });
  }

  setActive(id: number, isActive: boolean): Observable<User> {
    return this.http.patch<User>(`${this.apiUrl}/${id}/active`, { isActive });
  }
}
