import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../core/services/order.service';
import { CourierService } from '../../core/services/courier.service';
import { WebSocketService } from '../../core/services/websocket.service';
import { OrderResponse, OrderStopResponse, PagedResult } from '../../core/models/order.models';
import { CourierDetailResponse } from '../../core/models/courier.models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LiveMapComponent, MapMarker } from '../../shared/components/map/live-map.component';

@Component({
  selector: 'app-dispatcher-console',
  standalone: true,
  imports: [CommonModule, FormsModule, StatusBadgeComponent, LiveMapComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dispatcher-container">
      <!-- Top Metrics Bar -->
      <div class="metrics-bar">
        <div class="metric-card">
          <span class="metric-label">ACTIVE ORDERS</span>
          <span class="metric-val text-blue">{{ activeOrdersCount() }}</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">IN TRANSIT</span>
          <span class="metric-val text-amber">{{ inTransitCount() }}</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">AVAILABLE COURIERS</span>
          <span class="metric-val text-green">{{ availableCouriersCount() }}</span>
        </div>
        <div class="metric-card">
          <span class="metric-label">FLEET TOTAL</span>
          <span class="metric-val text-slate">{{ couriers().length }}</span>
        </div>
      </div>

      <!-- Main Operations Grid -->
      <div class="ops-grid">
        <!-- Left: Orders Queue -->
        <div class="panel orders-panel">
          <div class="panel-header">
            <h3>⚡ Live Orders Stream</h3>
            <button class="btn-refresh" (click)="refreshAll()">↻ Refresh</button>
          </div>

          <div class="queue-list">
            @for (order of orders(); track order.id) {
              <div
                class="queue-item"
                [class.selected]="selectedOrder()?.id === order.id"
                (click)="selectOrder(order)"
              >
                <div class="queue-item-head">
                  <span class="order-no">{{ order.orderNo }}</span>
                  <app-status-badge [status]="order.status" />
                </div>
                <div class="queue-item-meta">
                  <span>🚗 {{ order.vehicleType || 'Any' }}</span>
                  <span>⚖ {{ order.packageWeight || 1 }} kg</span>
                  <span>⏱ {{ order.createdAt | date: 'HH:mm' }}</span>
                </div>
                <div class="queue-item-stops">
                  <div><strong>P:</strong> {{ order.stops[0]?.address }}</div>
                  <div><strong>D:</strong> {{ order.stops[1]?.address }}</div>
                </div>
              </div>
            } @empty {
              <div class="empty-state">No active orders in dispatch pipeline.</div>
            }
          </div>
        </div>

        <!-- Center: Fleet Radar Map -->
        <div class="panel map-panel">
          <div class="panel-header">
            <h3>🗺 Tactical Operations Map</h3>
            <span class="telemetry-badge">250ms Throttled Sync</span>
          </div>

          <div class="map-container-box">
            <app-live-map
              [markers]="combinedMarkers()"
              [polylineCoords]="orderRouteCoords()"
            />
          </div>
        </div>

        <!-- Right: Couriers Fleet List -->
        <div class="panel fleet-panel">
          <div class="panel-header">
            <h3>🛵 Fleet Status</h3>
            <span class="fleet-count">{{ couriers().length }} Registered</span>
          </div>

          <div class="fleet-list">
            @for (courier of couriers(); track courier.id) {
              <div class="courier-item">
                <div class="courier-item-top">
                  <span class="courier-name">{{ courier.fullName }}</span>
                  <app-status-badge [status]="courier.status" />
                </div>
                <div class="courier-sub">
                  <span>{{ courier.vehicle?.type || 'Car' }} ({{ courier.vehicle?.plateNumber || 'N/A' }})</span>
                  <span>★ {{ courier.rating | number: '1.1-1' }}</span>
                </div>

                <!-- Status Toggles -->
                <div class="status-actions">
                  @if (courier.status === 'Offline') {
                    <button class="btn-status btn-avail" (click)="setCourierStatus(courier, 'Available')">
                      Set Available
                    </button>
                  }
                  @if (courier.status === 'Available') {
                    <button class="btn-status btn-busy" (click)="setCourierStatus(courier, 'Busy')">
                      Set Busy
                    </button>
                    <button class="btn-status btn-off" (click)="setCourierStatus(courier, 'Offline')">
                      Set Offline
                    </button>
                  }
                  @if (courier.status === 'Busy') {
                    <button class="btn-status btn-avail" (click)="setCourierStatus(courier, 'Available')">
                      Release Busy
                    </button>
                  }
                </div>
              </div>
            } @empty {
              <div class="empty-state">No couriers registered yet.</div>
            }
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dispatcher-container {
      max-width: 1560px;
      margin: 0 auto;
      padding: 1.25rem 1.5rem;
    }
    .metrics-bar {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1rem;
      margin-bottom: 1.25rem;
    }
    .metric-card {
      background: #11192e;
      border: 1px solid #1e293b;
      padding: 1rem 1.25rem;
      border-radius: 0.65rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .metric-label {
      font-size: 0.7rem;
      font-weight: 700;
      color: #64748b;
      letter-spacing: 0.08em;
    }
    .metric-val {
      font-size: 1.75rem;
      font-weight: 800;
      font-family: monospace;
    }
    .text-blue { color: #60a5fa; }
    .text-amber { color: #fbbf24; }
    .text-green { color: #34d399; }
    .text-slate { color: #cbd5e1; }
    .ops-grid {
      display: grid;
      grid-template-columns: 340px 1fr 340px;
      gap: 1.25rem;
      align-items: start;
    }
    .panel {
      background: #11192e;
      border: 1px solid #1e293b;
      border-radius: 0.75rem;
      padding: 1rem;
    }
    .panel-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.75rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #1e293b;
    }
    .panel-header h3 {
      font-size: 0.9rem;
      font-weight: 700;
      color: #f8fafc;
      margin: 0;
    }
    .btn-refresh {
      background: #090e1a;
      border: 1px solid #1e293b;
      color: #94a3b8;
      font-size: 0.75rem;
      padding: 0.25rem 0.5rem;
      border-radius: 0.25rem;
      cursor: pointer;
    }
    .queue-list, .fleet-list {
      max-height: 680px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .queue-item, .courier-item {
      background: #090e1a;
      border: 1px solid #1e293b;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .queue-item:hover, .courier-item:hover {
      border-color: #334155;
    }
    .queue-item.selected {
      border-color: #10b981;
      background: rgba(16, 185, 129, 0.04);
    }
    .queue-item-head, .courier-item-top {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.4rem;
    }
    .order-no {
      font-family: monospace;
      font-size: 0.8rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .queue-item-meta, .courier-sub {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      font-size: 0.7rem;
      color: #94a3b8;
      margin-bottom: 0.4rem;
    }
    .queue-item-stops {
      font-size: 0.725rem;
      color: #94a3b8;
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      border-top: 1px dashed #1e293b;
      padding-top: 0.4rem;
    }
    .map-container-box {
      height: 680px;
    }
    .telemetry-badge {
      font-size: 0.65rem;
      color: #10b981;
      background: rgba(16, 185, 129, 0.1);
      padding: 0.15rem 0.4rem;
      border-radius: 0.25rem;
      font-weight: 600;
    }
    .courier-name {
      font-size: 0.825rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .fleet-count {
      font-size: 0.7rem;
      color: #64748b;
    }
    .status-actions {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      margin-top: 0.5rem;
      padding-top: 0.4rem;
      border-top: 1px solid #1e293b;
    }
    .btn-status {
      flex: 1;
      padding: 0.25rem;
      font-size: 0.675rem;
      font-weight: 600;
      border-radius: 0.25rem;
      border: 1px solid transparent;
      cursor: pointer;
    }
    .btn-avail {
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
      border-color: rgba(16, 185, 129, 0.3);
    }
    .btn-busy {
      background: rgba(245, 158, 11, 0.15);
      color: #fbbf24;
      border-color: rgba(245, 158, 11, 0.3);
    }
    .btn-off {
      background: rgba(148, 163, 184, 0.15);
      color: #cbd5e1;
      border-color: rgba(148, 163, 184, 0.3);
    }
    .empty-state {
      padding: 2rem 1rem;
      text-align: center;
      color: #64748b;
      font-size: 0.8rem;
    }
  `],
})
export class DispatcherConsoleComponent implements OnInit {
  private readonly orderService = inject(OrderService);
  private readonly courierService = inject(CourierService);
  private readonly wsService = inject(WebSocketService);

  readonly orders = signal<OrderResponse[]>([]);
  readonly couriers = signal<CourierDetailResponse[]>([]);
  readonly selectedOrder = signal<OrderResponse | null>(null);

  readonly activeOrdersCount = computed(() =>
    this.orders().filter((o) => o.status !== 'Delivered' && o.status !== 'Cancelled').length
  );
  readonly inTransitCount = computed(() =>
    this.orders().filter((o) => o.status === 'PickedUp').length
  );
  readonly availableCouriersCount = computed(() =>
    this.couriers().filter((c) => c.status === 'Available').length
  );

  // Map markers: combine selected order stops + active available couriers
  readonly combinedMarkers = computed<MapMarker[]>(() => {
    const list: MapMarker[] = [];
    const o = this.selectedOrder();

    if (o && o.stops.length >= 2) {
      const p = o.stops.find((s: OrderStopResponse) => s.type === 'Pickup') || o.stops[0];
      const d = o.stops.find((s: OrderStopResponse) => s.type === 'Dropoff') || o.stops[1];

      list.push({
        id: 'p-' + o.id,
        type: 'pickup',
        latitude: p.latitude,
        longitude: p.longitude,
        title: `Order ${o.orderNo} Pickup`,
        subtitle: p.address,
      });

      list.push({
        id: 'd-' + o.id,
        type: 'dropoff',
        latitude: d.latitude,
        longitude: d.longitude,
        title: `Order ${o.orderNo} Dropoff`,
        subtitle: d.address,
      });
    }

    // Add couriers
    this.couriers().forEach((c: CourierDetailResponse, idx: number) => {
      // Offset slightly around New York for visual demonstration
      const baseLat = 40.7128 + (idx * 0.008 - 0.015);
      const baseLon = -74.0060 + (idx * 0.009 - 0.015);
      list.push({
        id: 'courier-' + c.id,
        type: 'courier',
        latitude: baseLat,
        longitude: baseLon,
        title: c.fullName,
        subtitle: `${c.vehicle?.type || 'Car'} - ${c.vehicle?.plateNumber || ''}`,
        status: c.status,
      });
    });

    return list;
  });

  readonly orderRouteCoords = computed<[number, number][]>(() => {
    const o = this.selectedOrder();
    if (!o || o.stops.length < 2) return [];
    return o.stops.map((s: OrderStopResponse) => [s.latitude, s.longitude] as [number, number]);
  });

  ngOnInit(): void {
    this.refreshAll();
    this.wsService.connect();
    this.wsService.subscribeToChannel('fleet');
  }

  refreshAll(): void {
    this.orderService.getOrders({ pageSize: 50 }).subscribe({
      next: (res: PagedResult<OrderResponse>) => {
        this.orders.set(res.items);
        if (res.items.length > 0 && !this.selectedOrder()) {
          this.selectedOrder.set(res.items[0]);
        }
      },
    });

    this.courierService.getCouriers({ pageSize: 50 }).subscribe({
      next: (res: PagedResult<CourierDetailResponse>) => this.couriers.set(res.items),
    });
  }

  selectOrder(order: OrderResponse): void {
    this.selectedOrder.set(order);
  }

  setCourierStatus(courier: CourierDetailResponse, status: 'Available' | 'Busy' | 'Offline'): void {
    this.courierService.updateStatus(courier.id, status).subscribe({
      next: (updated: CourierDetailResponse) => {
        this.couriers.update((list: CourierDetailResponse[]) =>
          list.map((c) => (c.id === updated.id ? updated : c))
        );
      },
      error: (err: any) => alert(err.error?.detail || 'Status transition rejected.'),
    });
  }
}
