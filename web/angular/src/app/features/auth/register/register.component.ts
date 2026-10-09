import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { RegisterRequest } from '../../../core/models/auth.models';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="register-page">
      <div class="register-card">
        <div class="card-header">
          <h2>Create Platform Account</h2>
          <p>Register as a delivery customer or apply as a fleet courier</p>
        </div>

        @if (errorMessage()) {
          <div class="error-banner">
            {{ errorMessage() }}
          </div>
        }

        <form (ngSubmit)="onSubmit()" class="register-form">
          <!-- Role Selector -->
          <div class="role-selector">
            <button
              type="button"
              class="role-btn"
              [class.active]="role === 'Customer'"
              (click)="role = 'Customer'"
            >
              📦 Customer
            </button>
            <button
              type="button"
              class="role-btn"
              [class.active]="role === 'Courier'"
              (click)="role = 'Courier'"
            >
              🛵 Fleet Courier
            </button>
          </div>

          <div class="form-group">
            <label for="fullName">Full Name</label>
            <input
              id="fullName"
              type="text"
              name="fullName"
              [(ngModel)]="fullName"
              placeholder="e.g. Alex Morgan"
              required
            />
          </div>

          <div class="form-group">
            <label for="email">Email Address</label>
            <input
              id="email"
              type="email"
              name="email"
              [(ngModel)]="email"
              placeholder="alex@example.com"
              required
            />
          </div>

          <div class="form-group">
            <label for="phone">Phone Number</label>
            <input
              id="phone"
              type="tel"
              name="phone"
              [(ngModel)]="phone"
              placeholder="+1 555-0199"
            />
          </div>

          <div class="form-group">
            <label for="password">Password (min 8 chars)</label>
            <input
              id="password"
              type="password"
              name="password"
              [(ngModel)]="password"
              placeholder="••••••••••••"
              required
            />
          </div>

          @if (role === 'Courier') {
            <div class="courier-fields">
              <div class="form-group">
                <label for="vehicleType">Vehicle Type</label>
                <select id="vehicleType" name="vehicleType" [(ngModel)]="vehicleType">
                  <option value="Bike">Bicycle / E-Bike</option>
                  <option value="Car">Sedan / Hatchback</option>
                  <option value="Van">Cargo Van</option>
                  <option value="Truck">Box Truck</option>
                </select>
              </div>

              <div class="form-group">
                <label for="plateNumber">Plate / Identifier</label>
                <input
                  id="plateNumber"
                  type="text"
                  name="plateNumber"
                  [(ngModel)]="plateNumber"
                  placeholder="e.g. NYC-7842"
                  required
                />
              </div>
            </div>
          }

          <button type="submit" class="btn-submit" [disabled]="loading()">
            @if (loading()) {
              <span>Creating Account...</span>
            } @else {
              <span>Register Account</span>
            }
          </button>
        </form>

        <div class="card-footer">
          <span>Already registered?</span>
          <a routerLink="/auth/login">Sign in</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .register-page {
      min-height: calc(100vh - 65px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2.5rem 1rem;
      background: #0a0f1d;
    }
    .register-card {
      width: 100%;
      max-width: 480px;
      background: #11192e;
      border: 1px solid #1e293b;
      border-radius: 1rem;
      padding: 2.25rem;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
    }
    .card-header {
      text-align: center;
      margin-bottom: 1.75rem;
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
    .role-selector {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
      margin-bottom: 1.5rem;
    }
    .role-btn {
      padding: 0.75rem;
      background: #090e1a;
      border: 1px solid #1e293b;
      border-radius: 0.5rem;
      color: #94a3b8;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .role-btn.active {
      border-color: #10b981;
      color: #10b981;
      background: rgba(16, 185, 129, 0.08);
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
      margin-bottom: 1.15rem;
    }
    .form-group label {
      display: block;
      font-size: 0.8rem;
      font-weight: 600;
      color: #cbd5e1;
      margin-bottom: 0.4rem;
    }
    .form-group input, .form-group select {
      width: 100%;
      padding: 0.7rem 0.9rem;
      background: #090e1a;
      border: 1px solid #243048;
      border-radius: 0.5rem;
      color: #f8fafc;
      font-size: 0.9rem;
      outline: none;
      box-sizing: border-box;
    }
    .form-group input:focus, .form-group select:focus {
      border-color: #10b981;
    }
    .courier-fields {
      background: rgba(16, 185, 129, 0.04);
      padding: 1rem;
      border-radius: 0.5rem;
      border: 1px dashed rgba(16, 185, 129, 0.2);
      margin-bottom: 1.25rem;
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
      margin-top: 0.5rem;
    }
    .btn-submit:hover:not(:disabled) {
      background: #34d399;
    }
    .btn-submit:disabled {
      opacity: 0.6;
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
  `],
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  role: 'Customer' | 'Courier' = 'Customer';
  fullName = '';
  email = '';
  phone = '';
  password = '';
  vehicleType: 'Bike' | 'Car' | 'Van' | 'Truck' = 'Car';
  plateNumber = '';

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  onSubmit(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const payload: RegisterRequest = {
      role: this.role,
      fullName: this.fullName,
      email: this.email,
      phone: this.phone || undefined,
      password: this.password,
      vehicleType: this.role === 'Courier' ? this.vehicleType : undefined,
      plateNumber: this.role === 'Courier' ? this.plateNumber : undefined,
    };

    this.authService.register(payload).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (this.role === 'Courier') {
          // Awaiting admin approval per Agent.md
          alert('Courier registration submitted! Awaiting Administrator review.');
        }
        this.router.navigate([this.role === 'Customer' ? '/customer' : '/auth/login']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.detail || err.message || 'Registration failed.');
      },
    });
  }
}
