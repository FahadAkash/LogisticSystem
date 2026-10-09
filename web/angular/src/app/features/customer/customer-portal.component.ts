import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../core/services/order.service';
import { WebSocketService } from '../../core/services/websocket.service';
import { OrderResponse, CreateOrderRequest, OrderStopResponse } from '../../core/models/order.models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LiveMapComponent, MapMarker } from '../../shared/components/map/live-map.component';

@Component({
  selector: 'app-customer-portal',
  standalone: true,
  imports: [CommonModule, FormsModule, StatusBadgeComponent, LiveMapComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="customer-container">
      <!-- Header -->
      <div class="portal-header">
        <div>
          <h1>Customer Delivery Portal</h1>
          <p class="subtitle">Place instant on-demand orders and track deliveries in real time</p>
        </div>
        <button class="btn-refresh" (click)="loadOrders()" [disabled]="loadingOrders()">
          ↻ Refresh Orders
        </button>
      </div>

      <div class="portal-grid">
        <!-- Left: Create Order Form -->
        <div class="panel form-panel">
          <div class="panel-header">
            <h3>📦 New Dispatch Order</h3>
            <button type="button" class="btn-quick" (click)="prefillManhattan()">⚡ Quick Preset</button>
          </div>

          @if (orderSuccessMessage()) {
            <div class="success-banner">{{ orderSuccessMessage() }}</div>
          }
          @if (orderErrorMessage()) {
            <div class="error-banner">{{ orderErrorMessage() }}</div>
          }

          <form (ngSubmit)="createOrder()" class="order-form">
            <!-- Pickup Section -->
            <div class="stop-box pickup-box">
              <span class="stop-tag">1. PICKUP STOP</span>
              <div class="form-row">
                <input type="text" [(ngModel)]="pickupAddress" name="pAddr" placeholder="Pickup Address" required />
              </div>
              <div class="form-row-cols">
                <input type="number" step="0.0001" [(ngModel)]="pickupLat" name="pLat" placeholder="Latitude" required />
                <input type="number" step="0.0001" [(ngModel)]="pickupLon" name="pLon" placeholder="Longitude" required />
              </div>
              <div class="form-row">
                <input type="text" [(ngModel)]="pickupContact" name="pCont" placeholder="Contact Person" />
              </div>
            </div>

            <!-- Dropoff Section -->
            <div class="stop-box dropoff-box">
              <span class="stop-tag">2. DROPOFF STOP</span>
              <div class="form-row">
                <input type="text" [(ngModel)]="dropoffAddress" name="dAddr" placeholder="Delivery Address" required />
              </div>
              <div class="form-row-cols">
                <input type="number" step="0.0001" [(ngModel)]="dropoffLat" name="dLat" placeholder="Latitude" required />
                <input type="number" step="0.0001" [(ngModel)]="dropoffLon" name="dLon" placeholder="Longitude" required />
              </div>
              <div class="form-row">
                <input type="text" [(ngModel)]="dropoffContact" name="dCont" placeholder="Recipient Contact" />
              </div>
            </div>

            <!-- Package & Vehicle Options -->
            <div class="form-row-cols">
              <div>
                <label>Vehicle Constraint</label>
                <select [(ngModel)]="vehicleType" name="vType">
                  <option value="Car">Sedan / Car</option>
                  <option value="Bike">Bicycle</option>
                  <option value="Van">Cargo Van</option>
                  <option value="Truck">Box Truck</option>
                </select>
              </div>
              <div>
                <label>Weight (kg)</label>
                <input type="number" step="0.1" [(ngModel)]="packageWeight" name="pWeight" placeholder="e.g. 2.5" />
              </div>
            </div>

            <div class="form-row">
              <label>Package Description</label>
              <input type="text" [(ngModel)]="packageDescription" name="pDesc" placeholder="e.g. Urgent Legal Documents" />
            </div>

            <button type="submit" class="btn-create" [disabled]="submitting()">
              @if (submitting()) {
                <span>Placing Order (Enforcing Idempotency)...</span>
              } @else {
                <span>🚀 Place Order with Outbox Dispatch</span>
              }
            </button>
          </form>
        </div>

        <!-- Middle: Orders List -->
        <div class="panel orders-panel">
          <div class="panel-header">
            <h3>📑 Your Delivery Orders ({{ orders().length }})</h3>
          </div>

          <div class="orders-list">
            @if (loadingOrders()) {
              <div class="empty-state">Loading order ledger...</div>
            } @else if (orders().length === 0) {
              <div class="empty-state">No orders placed yet. Create your first delivery!</div>
            } @else {
              @for (order of orders(); track order.id) {
                <div
                  class="order-card"
                  [class.selected]="selectedOrder()?.id === order.id"
                  (click)="selectOrder(order)"
                >
                  <div class="order-card-header">
                    <span class="order-no">{{ order.orderNo }}</span>
                    <app-status-badge [status]="order.status" />
                  </div>

                  <div class="order-card-stops">
                    <div class="stop-line">
                      <span class="dot dot-pickup"></span>
                      <span class="addr">{{ order.stops[0]?.address || 'Pickup' }}</span>
                    </div>
                    <div class="stop-line">
                      <span class="dot dot-dropoff"></span>
                      <span class="addr">{{ order.stops[1]?.address || 'Dropoff' }}</span>
                    </div>
                  </div>

                  <div class="order-card-footer">
                    <span class="time">{{ order.createdAt | date: 'shortTime' }}</span>
                    @if (order.status === 'Created' || order.status === 'Assigned') {
                      <button
                        class="btn-cancel"
                        (click)="cancelOrder($event, order)"
                        title="Cancel Order"
                      >
                        Cancel
                      </button>
                    }
                  </div>
                </div>
              }
            }
          </div>
        </div>

        <!-- Right: Live Tracking & Map -->
        <div class="panel tracking-panel">
          <div class="panel-header">
            <h3>📡 Live Telemetry & Tracking</h3>
            @if (selectedOrder()) {
              <span class="selected-badge">{{ selectedOrder()?.orderNo }}</span>
            }
          </div>

          @if (selectedOrder()) {
            <div class="tracking-content">
              <!-- Live Map -->
              <div class="map-view">
                <app-live-map
                  [markers]="mapMarkers()"
                  [polylineCoords]="mapPolyline()"
                />
              </div>

              <!-- Stops Checklist -->
              <div class="tracking-details">
                <div class="status-tracker">
                  <div class="tracker-step" [class.done]="isStepDone(1)">
                    <div class="step-num">1</div>
                    <div class="step-info">
                      <strong>Order Placed</strong>
                      <span>Outbox Event Published</span>
                    </div>
                  </div>
                  <div class="tracker-step" [class.done]="isStepDone(2)">
                    <div class="step-num">2</div>
                    <div class="step-info">
                      <strong>Assigned</strong>
                      <span>{{ selectedOrder()?.assignedCourierId ? 'Courier Locked' : 'Matching...' }}</span>
                    </div>
                  </div>
                  <div class="tracker-step" [class.done]="isStepDone(3)">
                    <div class="step-num">3</div>
                    <div class="step-info">
                      <strong>In Transit</strong>
                      <span>Heading to Dropoff</span>
                    </div>
                  </div>
                  <div class="tracker-step" [class.done]="isStepDone(4)">
                    <div class="step-num">4</div>
                    <div class="step-info">
                      <strong>Delivered</strong>
                      <span>Confirmed by Courier</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          } @else {
            <div class="empty-state select-prompt">
              Select an order from the list to track its real-time telemetry and route.
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .customer-container {
      max-width: 1440px;
      margin: 0 auto;
      padding: 1.5rem;
    }
    .portal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1.5rem;
    }
    .portal-header h1 {
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
    .btn-refresh:hover {
      color: #f8fafc;
      border-color: #334155;
    }
    .portal-grid {
      display: grid;
      grid-template-columns: 360px 340px 1fr;
      gap: 1.25rem;
      align-items: start;
    }
    .panel {
      background: #11192e;
      border: 1px solid #1e293b;
      border-radius: 0.75rem;
      padding: 1.25rem;
      box-shadow: 0 4px 16px rgba(0,0,0,0.25);
    }
    .panel-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid #1e293b;
    }
    .panel-header h3 {
      font-size: 0.95rem;
      font-weight: 700;
      color: #f8fafc;
      margin: 0;
    }
    .btn-quick {
      background: rgba(59, 130, 246, 0.1);
      border: 1px solid rgba(59, 130, 246, 0.3);
      color: #60a5fa;
      font-size: 0.7rem;
      font-weight: 600;
      padding: 0.2rem 0.5rem;
      border-radius: 0.25rem;
      cursor: pointer;
    }
    .success-banner {
      background: rgba(16, 185, 129, 0.12);
      border: 1px solid rgba(16, 185, 129, 0.3);
      color: #34d399;
      padding: 0.5rem 0.75rem;
      border-radius: 0.375rem;
      font-size: 0.75rem;
      margin-bottom: 1rem;
    }
    .error-banner {
      background: rgba(239, 68, 68, 0.12);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: #f87171;
      padding: 0.5rem 0.75rem;
      border-radius: 0.375rem;
      font-size: 0.75rem;
      margin-bottom: 1rem;
    }
    .stop-box {
      padding: 0.75rem;
      border-radius: 0.5rem;
      margin-bottom: 0.75rem;
      border: 1px solid #1e293b;
      background: #090e1a;
    }
    .pickup-box { border-left: 3px solid #f59e0b; }
    .dropoff-box { border-left: 3px solid #3b82f6; }
    .stop-tag {
      font-size: 0.65rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      color: #94a3b8;
      display: block;
      margin-bottom: 0.4rem;
    }
    .form-row {
      margin-bottom: 0.5rem;
    }
    .form-row-cols {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .form-row-cols label, .form-row label {
      display: block;
      font-size: 0.7rem;
      color: #94a3b8;
      margin-bottom: 0.2rem;
    }
    input, select {
      width: 100%;
      padding: 0.5rem 0.65rem;
      background: #0f172a;
      border: 1px solid #243048;
      border-radius: 0.375rem;
      color: #f8fafc;
      font-size: 0.8rem;
      outline: none;
      box-sizing: border-box;
    }
    input:focus, select:focus {
      border-color: #10b981;
    }
    .btn-create {
      width: 100%;
      padding: 0.75rem;
      background: #10b981;
      color: #042f2e;
      font-weight: 700;
      font-size: 0.85rem;
      border: none;
      border-radius: 0.375rem;
      cursor: pointer;
      margin-top: 0.5rem;
      transition: all 0.15s ease;
    }
    .btn-create:hover:not(:disabled) {
      background: #34d399;
    }
    .btn-create:disabled {
      opacity: 0.5;
    }
    .orders-list {
      max-height: 640px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 0.65rem;
    }
    .order-card {
      background: #090e1a;
      border: 1px solid #1e293b;
      border-radius: 0.5rem;
      padding: 0.85rem;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .order-card:hover {
      border-color: #334155;
      background: #0f172a;
    }
    .order-card.selected {
      border-color: #10b981;
      background: rgba(16, 185, 129, 0.04);
    }
    .order-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 0.6rem;
    }
    .order-no {
      font-weight: 700;
      font-size: 0.8rem;
      color: #f1f5f9;
      font-family: monospace;
    }
    .order-card-stops {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .stop-line {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .dot {
      width: 0.45rem;
      height: 0.45rem;
      border-radius: 50%;
      flex-shrink: 0;
    }
    .dot-pickup { background: #f59e0b; }
    .dot-dropoff { background: #3b82f6; }
    .addr {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .order-card-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: 0.6rem;
      padding-top: 0.5rem;
      border-top: 1px solid #1e293b;
    }
    .time {
      font-size: 0.7rem;
      color: #64748b;
    }
    .btn-cancel {
      background: transparent;
      border: 1px solid rgba(239, 68, 68, 0.4);
      color: #f87171;
      font-size: 0.65rem;
      font-weight: 600;
      padding: 0.15rem 0.5rem;
      border-radius: 0.25rem;
      cursor: pointer;
    }
    .btn-cancel:hover {
      background: rgba(239, 68, 68, 0.15);
    }
    .selected-badge {
      background: rgba(16, 185, 129, 0.1);
      color: #10b981;
      padding: 0.15rem 0.5rem;
      border-radius: 0.25rem;
      font-size: 0.75rem;
      font-family: monospace;
      font-weight: 700;
    }
    .tracking-content {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .map-view {
      height: 380px;
    }
    .status-tracker {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 0.5rem;
      background: #090e1a;
      padding: 0.85rem;
      border-radius: 0.5rem;
      border: 1px solid #1e293b;
    }
    .tracker-step {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      opacity: 0.4;
    }
    .tracker-step.done {
      opacity: 1;
    }
    .step-num {
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 50%;
      background: #1e293b;
      color: #94a3b8;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.75rem;
      font-weight: 700;
    }
    .tracker-step.done .step-num {
      background: #10b981;
      color: #042f2e;
    }
    .step-info {
      display: flex;
      flex-direction: column;
    }
    .step-info strong {
      font-size: 0.725rem;
      color: #f1f5f9;
    }
    .step-info span {
      font-size: 0.65rem;
      color: #64748b;
    }
    .empty-state {
      padding: 2rem;
      text-align: center;
      color: #64748b;
      font-size: 0.825rem;
    }
  `],
})
export class CustomerPortalComponent implements OnInit {
  private readonly orderService = inject(OrderService);
  private readonly wsService = inject(WebSocketService);

  readonly orders = signal<OrderResponse[]>([]);
  readonly selectedOrder = signal<OrderResponse | null>(null);
  readonly loadingOrders = signal(false);
  readonly submitting = signal(false);
  readonly orderSuccessMessage = signal<string | null>(null);
  readonly orderErrorMessage = signal<string | null>(null);

  // Form State
  pickupAddress = 'Times Square, New York, NY';
  pickupLat = 40.7580;
  pickupLon = -73.9855;
  pickupContact = 'Alice Dispatcher';

  dropoffAddress = '100 Wall Street, New York, NY';
  dropoffLat = 40.7060;
  dropoffLon = -74.0090;
  dropoffContact = 'Bob Receiver';

  vehicleType: 'Bike' | 'Car' | 'Van' | 'Truck' = 'Car';
  packageWeight = 2.0;
  packageDescription = 'Rush Corporate Documents';

  // Computed Map Coordinates
  readonly mapMarkers = computed<MapMarker[]>(() => {
    const o = this.selectedOrder();
    if (!o || o.stops.length < 2) return [];

    const pickup = o.stops.find((s: OrderStopResponse) => s.type === 'Pickup') || o.stops[0];
    const dropoff = o.stops.find((s: OrderStopResponse) => s.type === 'Dropoff') || o.stops[1];

    const markers: MapMarker[] = [
      {
        id: 'stop-pickup',
        type: 'pickup',
        latitude: pickup.latitude,
        longitude: pickup.longitude,
        title: 'Pickup Location',
        subtitle: pickup.address,
      },
      {
        id: 'stop-dropoff',
        type: 'dropoff',
        latitude: dropoff.latitude,
        longitude: dropoff.longitude,
        title: 'Dropoff Destination',
        subtitle: dropoff.address,
      },
    ];

    if (o.assignedCourierId) {
      // Simulate courier location near pickup for active tracking demonstration
      const midLat = (pickup.latitude + dropoff.latitude) / 2;
      const midLon = (pickup.longitude + dropoff.longitude) / 2;
      markers.push({
        id: 'courier-loc',
        type: 'courier',
        latitude: midLat,
        longitude: midLon,
        title: 'Active Courier',
        subtitle: o.vehicleType || 'Vehicle',
        status: o.status,
      });
    }

    return markers;
  });

  readonly mapPolyline = computed<[number, number][]>(() => {
    const o = this.selectedOrder();
    if (!o || o.stops.length < 2) return [];
    return o.stops.map((s: OrderStopResponse) => [s.latitude, s.longitude] as [number, number]);
  });

  ngOnInit(): void {
    this.loadOrders();
    this.wsService.connect();
  }

  loadOrders(): void {
    this.loadingOrders.set(true);
    this.orderService.getOrders({ pageSize: 20 }).subscribe({
      next: (res) => {
        this.loadingOrders.set(false);
        this.orders.set(res.items);
        if (res.items.length > 0 && !this.selectedOrder()) {
          this.selectOrder(res.items[0]);
        }
      },
      error: () => this.loadingOrders.set(false),
    });
  }

  selectOrder(order: OrderResponse): void {
    this.selectedOrder.set(order);
    this.wsService.subscribeToChannel(`order:${order.id}`);
  }

  prefillManhattan(): void {
    this.pickupAddress = 'Herald Square, 34th St, New York';
    this.pickupLat = 40.7505;
    this.pickupLon = -73.9880;

    this.dropoffAddress = 'Battery Park, New York';
    this.dropoffLat = 40.7033;
    this.dropoffLon = -74.0170;

    this.packageDescription = 'High Priority Medical Package';
    this.packageWeight = 1.2;
    this.vehicleType = 'Bike';
  }

  createOrder(): void {
    this.submitting.set(true);
    this.orderSuccessMessage.set(null);
    this.orderErrorMessage.set(null);

    const payload: CreateOrderRequest = {
      priority: 1,
      vehicleType: this.vehicleType,
      packageDescription: this.packageDescription,
      packageWeight: this.packageWeight,
      stops: [
        {
          sequence: 1,
          type: 'Pickup',
          address: this.pickupAddress,
          latitude: this.pickupLat,
          longitude: this.pickupLon,
          contactName: this.pickupContact,
        },
        {
          sequence: 2,
          type: 'Dropoff',
          address: this.dropoffAddress,
          latitude: this.dropoffLat,
          longitude: this.dropoffLon,
          contactName: this.dropoffContact,
        },
      ],
    };

    this.orderService.createOrder(payload).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.orderSuccessMessage.set(`Order ${created.orderNo} dispatched! Outbox event published.`);
        this.orders.update((list) => [created, ...list]);
        this.selectOrder(created);
      },
      error: (err) => {
        this.submitting.set(false);
        this.orderErrorMessage.set(err.error?.detail || err.message || 'Failed to place order.');
      },
    });
  }

  cancelOrder(event: Event, order: OrderResponse): void {
    event.stopPropagation();
    if (!confirm(`Are you sure you want to cancel order ${order.orderNo}?`)) {
      return;
    }

    this.orderService.cancelOrder(order.id, 'Customer requested cancellation').subscribe({
      next: (updated) => {
        this.orders.update((list) => list.map((o) => (o.id === updated.id ? updated : o)));
        if (this.selectedOrder()?.id === updated.id) {
          this.selectedOrder.set(updated);
        }
      },
      error: (err) => alert(err.error?.detail || 'Cancellation failed.'),
    });
  }

  isStepDone(step: number): boolean {
    const s = this.selectedOrder()?.status;
    if (!s) return false;
    if (step === 1) return true; // Created
    if (step === 2) return s === 'Assigned' || s === 'PickedUp' || s === 'Delivered';
    if (step === 3) return s === 'PickedUp' || s === 'Delivered';
    if (step === 4) return s === 'Delivered';
    return false;
  }
}
