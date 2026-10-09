import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="badge" [ngClass]="badgeClass">
      <span class="indicator"></span>
      {{ status() }}
    </span>
  `,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;
      letter-spacing: 0.025em;
      border: 1px solid transparent;
      white-space: nowrap;
    }
    .indicator {
      width: 0.4rem;
      height: 0.4rem;
      border-radius: 50%;
    }

    /* Emerald */
    .status-available, .status-delivered, .status-active {
      background: rgba(16, 185, 129, 0.12);
      color: #34d399;
      border-color: rgba(16, 185, 129, 0.3);
    }
    .status-available .indicator, .status-delivered .indicator, .status-active .indicator {
      background: #10b981;
      box-shadow: 0 0 6px rgba(16, 185, 129, 0.6);
    }

    /* Amber */
    .status-created, .status-searching, .status-pendingapproval, .status-pending {
      background: rgba(245, 158, 11, 0.12);
      color: #fbbf24;
      border-color: rgba(245, 158, 11, 0.3);
    }
    .status-created .indicator, .status-searching .indicator, .status-pendingapproval .indicator, .status-pending .indicator {
      background: #f59e0b;
      box-shadow: 0 0 6px rgba(245, 158, 11, 0.6);
    }

    /* Blue / Indigo */
    .status-assigned, .status-pickedup, .status-busy {
      background: rgba(59, 130, 246, 0.12);
      color: #60a5fa;
      border-color: rgba(59, 130, 246, 0.3);
    }
    .status-assigned .indicator, .status-pickedup .indicator, .status-busy .indicator {
      background: #3b82f6;
      box-shadow: 0 0 6px rgba(59, 130, 246, 0.6);
    }

    /* Rose */
    .status-cancelled, .status-failed, .status-suspended {
      background: rgba(239, 68, 68, 0.12);
      color: #f87171;
      border-color: rgba(239, 68, 68, 0.3);
    }
    .status-cancelled .indicator, .status-failed .indicator, .status-suspended .indicator {
      background: #ef4444;
    }

    /* Slate (Default / Offline) */
    .status-offline, .status-unknown {
      background: rgba(148, 163, 184, 0.12);
      color: #cbd5e1;
      border-color: rgba(148, 163, 184, 0.2);
    }
    .status-offline .indicator, .status-unknown .indicator {
      background: #94a3b8;
    }
  `]
})
export class StatusBadgeComponent {
  readonly status = input.required<string>();

  get badgeClass(): string {
    const s = (this.status() || '').toLowerCase().replace(/\s+/g, '');
    return `status-${s}`;
  }
}
