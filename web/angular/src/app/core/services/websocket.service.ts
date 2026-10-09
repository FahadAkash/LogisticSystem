import { Injectable, inject, signal } from '@angular/core';
import { Subject, Observable, auditTime, filter } from 'rxjs';
import { AuthService } from './auth.service';
import { TrackingUpdate, WsMessage } from '../models/websocket.models';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'error';

@Injectable({
  providedIn: 'root',
})
export class WebSocketService {
  private readonly authService = inject(AuthService);

  readonly connectionState = signal<ConnectionState>('disconnected');
  readonly lastError = signal<string | null>(null);

  private socket: WebSocket | null = null;
  private readonly messageSubject = new Subject<WsMessage<unknown>>();
  private readonly locationSubject = new Subject<TrackingUpdate>();
  private readonly subscribedChannels = new Set<string>();

  private reconnectAttempts = 0;
  private readonly maxReconnectDelay = 30000;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;

  // Throttled location updates stream (Agent.md Rule 6: Throttle map updates)
  readonly throttledLocations$: Observable<TrackingUpdate> = this.locationSubject.pipe(
    auditTime(250)
  );

  connect(): void {
    if (this.socket && (this.socket.readyState === WebSocket.OPEN || this.socket.readyState === WebSocket.CONNECTING)) {
      return;
    }

    if (!this.authService.isAuthenticated()) {
      return;
    }

    this.connectionState.set('connecting');

    // 1. Obtain ticket from dotnet-api
    this.authService.getWebSocketTicket().subscribe({
      next: (ticketRes) => {
        this.establishConnection(ticketRes.ticket);
      },
      error: (err) => {
        this.connectionState.set('error');
        this.lastError.set('Failed to acquire WebSocket ticket: ' + (err.message || 'Error'));
        this.scheduleReconnect();
      },
    });
  }

  private establishConnection(ticket: string): void {
    try {
      const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
      const host = window.location.hostname;
      const port = '8083'; // go-gateway port
      const url = `${protocol}//${host}:${port}/ws?ticket=${encodeURIComponent(ticket)}`;

      this.socket = new WebSocket(url);

      this.socket.onopen = () => {
        this.connectionState.set('connected');
        this.reconnectAttempts = 0;
        this.lastError.set(null);

        // Resubscribe all active channels upon reconnect
        for (const channel of this.subscribedChannels) {
          this.sendFrame({ action: 'subscribe', channel });
        }
      };

      this.socket.onmessage = (event: MessageEvent) => {
        try {
          const msg: WsMessage<unknown> = JSON.parse(event.data);
          this.messageSubject.next(msg);

          if (msg.type === 'location.updated' || msg.type === 'courier.location') {
            this.locationSubject.next(msg.payload as TrackingUpdate);
          }
        } catch {
          // Ignore invalid frames
        }
      };

      this.socket.onclose = () => {
        this.connectionState.set('disconnected');
        this.scheduleReconnect();
      };

      this.socket.onerror = (err) => {
        this.connectionState.set('error');
        this.lastError.set('WebSocket connection error');
      };
    } catch (e: any) {
      this.connectionState.set('error');
      this.lastError.set(e.message || 'Socket init error');
      this.scheduleReconnect();
    }
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer) return;

    // Exponential backoff with jitter: 1s, 2s, 4s, 8s ... max 30s
    const baseDelay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), this.maxReconnectDelay);
    const delay = baseDelay + Math.floor(Math.random() * 500);
    this.reconnectAttempts++;

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      if (this.authService.isAuthenticated()) {
        this.connect();
      }
    }, delay);
  }

  subscribeToChannel(channel: string): void {
    this.subscribedChannels.add(channel);
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.sendFrame({ action: 'subscribe', channel });
    }
  }

  unsubscribeFromChannel(channel: string): void {
    this.subscribedChannels.delete(channel);
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.sendFrame({ action: 'unsubscribe', channel });
    }
  }

  messagesForChannel<T = unknown>(channel: string): Observable<WsMessage<T>> {
    return this.messageSubject.pipe(
      filter((msg) => msg.channel === channel)
    ) as Observable<WsMessage<T>>;
  }

  private sendFrame(data: unknown): void {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(JSON.stringify(data));
    }
  }

  disconnect(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    this.subscribedChannels.clear();
    if (this.socket) {
      this.socket.close();
      this.socket = null;
    }
    this.connectionState.set('disconnected');
  }

  // Fallback simulator for development/demo when go-gateway / loadtest is idle
  emitMockLocation(update: TrackingUpdate): void {
    this.locationSubject.next(update);
  }
}
