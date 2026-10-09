import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { WebSocketService } from '../../../core/services/websocket.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header-nav">
      <div class="nav-container">
        <!-- Brand -->
        <div class="brand">
          <div class="brand-badge">⚡</div>
          <div class="brand-text">
            <span class="brand-title">DISPATCH</span>
            <span class="brand-sub">CONTROL PLANE</span>
          </div>
        </div>

        <!-- Navigation Links -->
        @if (authService.isAuthenticated()) {
          <nav class="nav-links">
            @if (authService.isCustomer()) {
              <a routerLink="/customer" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" class="nav-item">
                Customer Portal
              </a>
            }
            @if (authService.isDispatcher()) {
              <a routerLink="/dispatcher" routerLinkActive="active" class="nav-item">
                Dispatcher Console
              </a>
            }
            @if (authService.isAdmin()) {
              <a routerLink="/admin" routerLinkActive="active" class="nav-item">
                Fleet Admin
              </a>
            }
          </nav>
        }

        <!-- Actions & Status -->
        <div class="nav-actions">
          <!-- Realtime Gateway Indicator -->
          <div class="ws-indicator" [attr.data-state]="wsService.connectionState()">
            <span class="pulse-dot"></span>
            <span class="ws-label">
              @switch (wsService.connectionState()) {
                @case ('connected') { TELEMETRY ACTIVE }
                @case ('connecting') { CONNECTING... }
                @default { STREAM STANDBY }
              }
            </span>
          </div>

          @if (authService.isAuthenticated()) {
            <div class="user-profile">
              <div class="user-meta">
                <span class="user-name">{{ authService.currentUser()?.fullName }}</span>
                <span class="role-badge">{{ authService.roles()[0] }}</span>
              </div>
              <button (click)="logout()" class="btn-logout" title="Sign Out">
                Sign Out
              </button>
            </div>
          } @else {
            <div class="auth-btns">
              <a routerLink="/auth/login" class="btn-login">Sign In</a>
              <a routerLink="/auth/register" class="btn-register">Register</a>
            </div>
          }
        </div>
      </div>
    </header>
  `,
  styles: [`
    .header-nav {
      background: #0b1120;
      border-bottom: 1px solid #1e293b;
      position: sticky;
      top: 0;
      z-index: 1000;
    }
    .nav-container {
      max-width: 1440px;
      margin: 0 auto;
      padding: 0.75rem 1.5rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1.5rem;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      text-decoration: none;
    }
    .brand-badge {
      width: 2.25rem;
      height: 2.25rem;
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      border-radius: 0.5rem;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.1rem;
      box-shadow: 0 0 16px rgba(16, 185, 129, 0.3);
    }
    .brand-text {
      display: flex;
      flex-direction: column;
    }
    .brand-title {
      font-weight: 800;
      letter-spacing: 0.08em;
      font-size: 1rem;
      color: #f8fafc;
    }
    .brand-sub {
      font-size: 0.65rem;
      color: #64748b;
      letter-spacing: 0.12em;
      font-weight: 600;
    }
    .nav-links {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .nav-item {
      padding: 0.45rem 0.85rem;
      border-radius: 0.375rem;
      color: #94a3b8;
      font-size: 0.825rem;
      font-weight: 500;
      text-decoration: none;
      transition: all 0.15s ease;
    }
    .nav-item:hover {
      color: #f8fafc;
      background: rgba(255, 255, 255, 0.04);
    }
    .nav-item.active {
      color: #10b981;
      background: rgba(16, 185, 129, 0.1);
      font-weight: 600;
    }
    .nav-actions {
      display: flex;
      align-items: center;
      gap: 1.25rem;
    }
    .ws-indicator {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.25rem 0.6rem;
      border-radius: 9999px;
      font-size: 0.675rem;
      font-weight: 700;
      letter-spacing: 0.05em;
      background: #0f172a;
      border: 1px solid #1e293b;
      color: #64748b;
    }
    .pulse-dot {
      width: 0.45rem;
      height: 0.45rem;
      border-radius: 50%;
      background: #64748b;
    }
    .ws-indicator[data-state='connected'] {
      color: #10b981;
      border-color: rgba(16, 185, 129, 0.3);
      background: rgba(16, 185, 129, 0.08);
    }
    .ws-indicator[data-state='connected'] .pulse-dot {
      background: #10b981;
      box-shadow: 0 0 8px #10b981;
    }
    .ws-indicator[data-state='connecting'] {
      color: #f59e0b;
      border-color: rgba(245, 158, 11, 0.3);
    }
    .ws-indicator[data-state='connecting'] .pulse-dot {
      background: #f59e0b;
    }
    .user-profile {
      display: flex;
      align-items: center;
      gap: 0.85rem;
    }
    .user-meta {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
    }
    .user-name {
      font-size: 0.8rem;
      font-weight: 600;
      color: #f1f5f9;
    }
    .role-badge {
      font-size: 0.65rem;
      color: #10b981;
      text-transform: uppercase;
      font-weight: 700;
      letter-spacing: 0.05em;
    }
    .btn-logout {
      background: transparent;
      border: 1px solid #334155;
      color: #94a3b8;
      padding: 0.35rem 0.75rem;
      border-radius: 0.375rem;
      font-size: 0.75rem;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .btn-logout:hover {
      background: rgba(239, 68, 68, 0.1);
      color: #ef4444;
      border-color: rgba(239, 68, 68, 0.3);
    }
    .auth-btns {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .btn-login {
      color: #cbd5e1;
      font-size: 0.825rem;
      text-decoration: none;
      padding: 0.4rem 0.8rem;
      border-radius: 0.375rem;
    }
    .btn-register {
      background: #10b981;
      color: #042f2e;
      font-size: 0.825rem;
      font-weight: 600;
      text-decoration: none;
      padding: 0.4rem 0.85rem;
      border-radius: 0.375rem;
      box-shadow: 0 0 12px rgba(16, 185, 129, 0.25);
    }
  `],
})
export class NavbarComponent {
  readonly authService = inject(AuthService);
  readonly wsService = inject(WebSocketService);
  private readonly router = inject(Router);

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}
