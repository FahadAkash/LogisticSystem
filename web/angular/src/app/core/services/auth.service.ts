import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  UserProfile,
  WebSocketTicketResponse,
} from '../models/auth.models';

const ACCESS_TOKEN_KEY = 'logistic_access_token';
const REFRESH_TOKEN_KEY = 'logistic_refresh_token';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly currentUser = signal<UserProfile | null>(null);
  readonly isAuthenticated = computed(() => !!this.currentUser());
  readonly roles = computed(() => this.currentUser()?.roles ?? []);

  readonly isAdmin = computed(() => this.roles().includes('Admin'));
  readonly isDispatcher = computed(() => this.roles().includes('Dispatcher') || this.roles().includes('Admin'));
  readonly isCourier = computed(() => this.roles().includes('Courier'));
  readonly isCustomer = computed(() => this.roles().includes('Customer'));

  constructor() {
    this.restoreSession();
  }

  private restoreSession(): void {
    const token = this.getAccessToken();
    if (token) {
      this.fetchMe().subscribe({
        error: () => this.clearTokens(),
      });
    }
  }

  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', credentials).pipe(
      tap((res) => {
        this.saveTokens(res.accessToken, res.refreshToken);
        this.currentUser.set(res.user);
      })
    );
  }

  register(data: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', data).pipe(
      tap((res) => {
        this.saveTokens(res.accessToken, res.refreshToken);
        this.currentUser.set(res.user);
      })
    );
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post('/api/auth/logout', { refreshToken }).subscribe({
        error: () => {},
      });
    }
    this.clearTokens();
    this.currentUser.set(null);
  }

  fetchMe(): Observable<UserProfile> {
    return this.http.get<UserProfile>('/api/auth/me').pipe(
      tap((user) => this.currentUser.set(user))
    );
  }

  getWebSocketTicket(): Observable<WebSocketTicketResponse> {
    return this.http.post<WebSocketTicketResponse>('/api/auth/ws-ticket', {});
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  private saveTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  }

  private clearTokens(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  }
}

