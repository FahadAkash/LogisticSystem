import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CourierService } from '../../core/services/courier.service';
import { CourierDetailResponse } from '../../core/models/courier.models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="admin-container">
      <div class="admin-header">
        <div>
          <h1>Fleet Administration & Governance</h1>
          <p class="subtitle">Review courier onboarding applications and manage active operational fleet</p>
        </div>
        <button class="btn-refresh" (click)="loadAll()">↻ Refresh</button>
      </div>

      <!-- Pending Courier Approvals Section -->
      <div class="section-card">
        <div class="section-title-bar">
          <div class="section-title">
            <span class="alert-icon">⚠️</span>
            <h3>Pending Courier Applications ({{ pendingCouriers().length }})</h3>
          </div>
          <span class="subtext">Couriers awaiting identity and vehicle verification per Agent.md Rule 10.3</span>
        </div>

        @if (pendingCouriers().length === 0) {
          <div class="clean-state">
            <span class="check-icon">✓</span>
            <span>All courier applications have been reviewed. No pending approvals.</span>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="data-table">
              <thead>
                <tr>
                  <th>COURIER NAME</th>
                  <th>CONTACT</th>
                  <th>VEHICLE TYPE</th>
                  <th>PLATE NUMBER</th>
                  <th>APPLIED ON</th>
                  <th class="text-right">GOVERNANCE ACTION</th>
                </tr>
              </thead>
              <tbody>
                @for (c of pendingCouriers(); track c.id) {
                  <tr>
                    <td>
                      <div class="courier-profile-cell">
                        <span class="avatar">{{ c.fullName.charAt(0) }}</span>
                        <strong>{{ c.fullName }}</strong>
                      </div>
                    </td>
                    <td>
                      <div class="contact-cell">
                        <span>{{ c.email }}</span>
                        <span class="phone">{{ c.phone || 'No phone' }}</span>
                      </div>
                    </td>
                    <td>
                      <span class="vehicle-badge">{{ c.vehicle?.type || 'Standard' }}</span>
                    </td>
                    <td>
                      <code class="plate-code">{{ c.vehicle?.plateNumber || 'N/A' }}</code>
                    </td>
                    <td>
                      <span class="date-cell">{{ c.createdAt | date: 'mediumDate' }}</span>
                    </td>
                    <td class="text-right">
                      <button
                        class="btn-approve"
                        (click)="approveCourier(c)"
                        [disabled]="approvingId() === c.id"
                      >
                        @if (approvingId() === c.id) {
                          Approving...
                        } @else {
                          ✓ Approve Courier
                        }
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>

      <!-- Registered Fleet Directory Section -->
      <div class="section-card">
        <div class="section-title-bar">
          <div class="section-title">
            <span class="fleet-icon">🛵</span>
            <h3>Active Fleet Directory ({{ allCouriers().length }})</h3>
          </div>
        </div>

        <div class="table-responsive">
          <table class="data-table">
            <thead>
              <tr>
                <th>COURIER</th>
                <th>STATUS</th>
                <th>RATING</th>
                <th>VEHICLE</th>
                <th>ACTIVE ASSIGNMENT</th>
                <th>LAST UPDATED</th>
              </tr>
            </thead>
            <tbody>
              @for (c of allCouriers(); track c.id) {
                <tr>
                  <td>
                    <strong>{{ c.fullName }}</strong>
                    <div class="subtext">{{ c.email }}</div>
                  </td>
                  <td>
                    <app-status-badge [status]="c.status" />
                  </td>
                  <td>
                    <span class="rating">★ {{ c.rating | number: '1.1-1' }}</span>
                  </td>
                  <td>
                    {{ c.vehicle?.type }} ({{ c.vehicle?.plateNumber }})
                  </td>
                  <td>
                    @if (c.activeOrder) {
                      <span class="order-tag">{{ c.activeOrder.orderNo }}</span>
                    } @else {
                      <span class="text-muted">None</span>
                    }
                  </td>
                  <td>
                    {{ c.updatedAt | date: 'short' }}
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="6" class="text-center py-4">No couriers registered in the system.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .admin-container {
      max-width: 1440px;
      margin: 0 auto;
      padding: 1.5rem;
    }
    .admin-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1.5rem;
    }
    .admin-header h1 {
      font-size: 1.5rem;
      font-weight: 800;
      color: #f8fafc;
      margin-bottom: 0.25rem;
    }
    .subtitle {
      color: #94a3b8;
      font-size: 0.85rem;
    }
    .btn-refresh {
      background: #0f172a;
      border: 1px solid #1e293b;
      color: #94a3b8;
      padding: 0.45rem 0.9rem;
      border-radius: 0.375rem;
      font-size: 0.8rem;
      cursor: pointer;
    }
    .section-card {
      background: #11192e;
      border: 1px solid #1e293b;
      border-radius: 0.75rem;
      padding: 1.5rem;
      margin-bottom: 1.5rem;
    }
    .section-title-bar {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-bottom: 1.25rem;
    }
    .section-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .section-title h3 {
      font-size: 1.05rem;
      font-weight: 700;
      color: #f8fafc;
      margin: 0;
    }
    .subtext {
      font-size: 0.75rem;
      color: #64748b;
    }
    .clean-state {
      padding: 2rem;
      text-align: center;
      background: rgba(16, 185, 129, 0.04);
      border: 1px dashed rgba(16, 185, 129, 0.2);
      border-radius: 0.5rem;
      color: #34d399;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      font-size: 0.85rem;
    }
    .check-icon {
      font-weight: 800;
    }
    .table-responsive {
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.825rem;
      text-align: left;
    }
    .data-table th {
      padding: 0.75rem 1rem;
      color: #64748b;
      font-size: 0.675rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      border-bottom: 1px solid #1e293b;
    }
    .data-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #1e293b;
      color: #cbd5e1;
    }
    .courier-profile-cell {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .avatar {
      width: 1.75rem;
      height: 1.75rem;
      border-radius: 50%;
      background: #1e293b;
      display: flex;
      align-items: center;
      justify-content: center;
      color: #10b981;
      font-weight: 700;
      font-size: 0.75rem;
    }
    .contact-cell {
      display: flex;
      flex-direction: column;
    }
    .contact-cell .phone {
      font-size: 0.7rem;
      color: #64748b;
    }
    .vehicle-badge {
      background: #090e1a;
      border: 1px solid #1e293b;
      padding: 0.2rem 0.5rem;
      border-radius: 0.25rem;
      font-size: 0.75rem;
    }
    .plate-code {
      font-family: monospace;
      color: #60a5fa;
      background: rgba(59, 130, 246, 0.08);
      padding: 0.2rem 0.4rem;
      border-radius: 0.25rem;
    }
    .rating {
      color: #fbbf24;
      font-weight: 700;
    }
    .order-tag {
      font-family: monospace;
      font-size: 0.75rem;
      color: #34d399;
      background: rgba(16, 185, 129, 0.1);
      padding: 0.15rem 0.4rem;
      border-radius: 0.25rem;
    }
    .text-right {
      text-align: right;
    }
    .btn-approve {
      background: #10b981;
      color: #042f2e;
      border: none;
      padding: 0.45rem 0.9rem;
      border-radius: 0.375rem;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.15s ease;
      box-shadow: 0 0 12px rgba(16, 185, 129, 0.25);
    }
    .btn-approve:hover:not(:disabled) {
      background: #34d399;
    }
    .btn-approve:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .text-muted {
      color: #64748b;
    }
  `],
})
export class AdminPanelComponent implements OnInit {
  private readonly courierService = inject(CourierService);

  readonly pendingCouriers = signal<CourierDetailResponse[]>([]);
  readonly allCouriers = signal<CourierDetailResponse[]>([]);
  readonly approvingId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.courierService.getPendingCouriers().subscribe({
      next: (list) => this.pendingCouriers.set(list),
    });

    this.courierService.getCouriers({ pageSize: 100 }).subscribe({
      next: (res) => this.allCouriers.set(res.items),
    });
  }

  approveCourier(courier: CourierDetailResponse): void {
    this.approvingId.set(courier.id);
    this.courierService.approveCourier(courier.id).subscribe({
      next: (approved) => {
        this.approvingId.set(null);
        // Remove from pending
        this.pendingCouriers.update((list) => list.filter((c) => c.id !== courier.id));
        // Update in all couriers
        this.allCouriers.update((list) =>
          list.map((c) => (c.id === approved.id ? { ...c, status: approved.status } : c))
        );
      },
      error: (err) => {
        this.approvingId.set(null);
        alert(err.error?.detail || 'Approval failed.');
      },
    });
  }
}
