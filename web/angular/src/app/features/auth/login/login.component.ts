import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="card-header">
          <div class="badge-icon">⚡</div>
          <h2>Control Plane Sign In</h2>
          <p>Access the real-time logistics and dispatch operations console</p>
        </div>

        @if (errorMessage()) {
          <div class="error-banner">
            {{ errorMessage() }}
          </div>
        }

        <form (ngSubmit)="onSubmit()" class="login-form">
          <div class="form-group">
            <label for="email">Work Email</label>
            <input
              id="email"
              type="email"
              name="email"
              [(ngModel)]="email"
              placeholder="e.g. admin@logistic.local"
              required
              autocomplete="email"
            />
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              id="password"
              type="password"
              name="password"
              [(ngModel)]="password"
              placeholder="••••••••••••"
              required
              autocomplete="current-password"
            />
          </div>

          <button type="submit" class="btn-submit" [disabled]="loading()">
            @if (loading()) {
              <span>Authenticating...</span>
            } @else {
              <span>Authorize & Enter</span>
            }
          </button>
        </form>

        <!-- Quick Demo Credentials Presets -->
        <div class="demo-presets">
          <div class="presets-title">1-Click Test Credentials</div>
          <div class="presets-grid">
            <button type="button" (click)="fillCredentials('admin@logistic.local', 'Admin1234!')" class="btn-preset">
              <strong>Admin</strong>
              <span>Fleet & Couriers</span>
            </button>
            <button type="button" (click)="fillCredentials('dispatcher@logistic.local', 'Admin1234!')" class="btn-preset">
              <strong>Dispatcher</strong>
              <span>Live Console</span>
            </button>
            <button type="button" (click)="fillCredentials('customer@example.com', 'CustomerSecurePass123!')" class="btn-preset">
              <strong>Customer</strong>
              <span>Order & Track</span>
            </button>
          </div>
        </div>

        <div class="card-footer">
          <span>Need an account?</span>
          <a routerLink="/auth/register">Register Customer or Courier</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: calc(100vh - 65px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem 1rem;
      background: #0a0f1d;
    }
    .login-card {
      width: 100%;
      max-width: 440px;
      background: #11192e;
      border: 1px solid #1e293b;
      border-radius: 1rem;
      padding: 2.25rem;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
    }
    .card-header {
      text-align: center;
      margin-bottom: 2rem;
    }
    .badge-icon {
      width: 2.75rem;
      height: 2.75rem;
      margin: 0 auto 1rem;
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      border-radius: 0.65rem;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.4rem;
      box-shadow: 0 0 20px rgba(16, 185, 129, 0.35);
    }
    .card-header h2 {
      font-size: 1.35rem;
      font-weight: 700;
      color: #f8fafc;
      margin-bottom: 0.35rem;
    }
    .card-header p {
      color: #94a3b8;
      font-size: 0.85rem;
    }
    .error-banner {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: #f87171;
      padding: 0.75rem 1rem;
      border-radius: 0.5rem;
      font-size: 0.825rem;
      margin-bottom: 1.5rem;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    .form-group label {
      display: block;
      font-size: 0.8rem;
      font-weight: 600;
      color: #cbd5e1;
      margin-bottom: 0.5rem;
    }
    .form-group input {
      width: 100%;
      padding: 0.75rem 1rem;
      background: #090e1a;
      border: 1px solid #243048;
      border-radius: 0.5rem;
      color: #f8fafc;
      font-size: 0.9rem;
      outline: none;
      transition: border-color 0.15s ease;
      box-sizing: border-box;
    }
    .form-group input:focus {
      border-color: #10b981;
      box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.2);
    }
    .btn-submit {
      width: 100%;
      padding: 0.85rem;
      background: #10b981;
      color: #042f2e;
      font-weight: 700;
      font-size: 0.925rem;
      border: none;
      border-radius: 0.5rem;
      cursor: pointer;
      transition: all 0.15s ease;
      margin-top: 0.5rem;
      box-shadow: 0 0 16px rgba(16, 185, 129, 0.3);
    }
    .btn-submit:hover:not(:disabled) {
      background: #34d399;
      transform: translateY(-1px);
    }
    .btn-submit:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .demo-presets {
      margin-top: 1.75rem;
      padding-top: 1.5rem;
      border-top: 1px solid #1e293b;
    }
    .presets-title {
      font-size: 0.725rem;
      font-weight: 700;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      margin-bottom: 0.75rem;
      text-align: center;
    }
    .presets-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.5rem;
    }
    .btn-preset {
      background: #090e1a;
      border: 1px solid #1e293b;
      padding: 0.5rem 0.25rem;
      border-radius: 0.375rem;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      transition: all 0.15s ease;
    }
    .btn-preset strong {
      font-size: 0.75rem;
      color: #e2e8f0;
    }
    .btn-preset span {
      font-size: 0.65rem;
      color: #64748b;
    }
    .btn-preset:hover {
      border-color: #3b82f6;
      background: rgba(59, 130, 246, 0.08);
    }
    .card-footer {
      margin-top: 1.5rem;
      text-align: center;
      font-size: 0.825rem;
      color: #94a3b8;
    }
    .card-footer a {
      color: #10b981;
      text-decoration: none;
      font-weight: 600;
      margin-left: 0.35rem;
    }
    .card-footer a:hover {
      text-decoration: underline;
    }
  `],
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  email = 'admin@logistic.local';
  password = 'Admin1234!';
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  fillCredentials(emailVal: string, passVal: string): void {
    this.email = emailVal;
    this.password = passVal;
    this.errorMessage.set(null);
  }

  onSubmit(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: (res) => {
        this.loading.set(false);
        const returnUrl = this.route.snapshot.queryParams['returnUrl'];
        if (returnUrl) {
          this.router.navigateByUrl(returnUrl);
          return;
        }

        // Navigate based on primary role
        if (res.user.roles.includes('Admin')) {
          this.router.navigate(['/admin']);
        } else if (res.user.roles.includes('Dispatcher')) {
          this.router.navigate(['/dispatcher']);
        } else {
          this.router.navigate(['/customer']);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.detail || err.message || 'Invalid email or password.');
      },
    });
  }
}
