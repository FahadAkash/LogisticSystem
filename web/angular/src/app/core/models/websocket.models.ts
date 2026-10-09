export interface WsMessage<T = unknown> {
  type: string; // 'location.updated' | 'order.status_changed' | 'offer.received'
  channel: string;
  timestamp: string;
  payload: T;
}

export interface TrackingUpdate {
  order_id?: string;
  courier_id: string;
  latitude: number;
  longitude: number;
  heading?: number;
  speed_kmh?: number;
  timestamp: string;
}

export interface OfferNotification {
  offer_id: string;
  order_id: string;
  pickup_address: string;
  pickup_latitude: number;
  pickup_longitude: number;
  expires_at: string;
  ttl_seconds: number;
}
