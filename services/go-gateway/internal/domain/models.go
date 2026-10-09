package domain

import (
	"time"

	"github.com/google/uuid"
)

// WsMessage defines standard WebSocket message format for clients
type WsMessage struct {
	Type      string      `json:"type"` // e.g. location.updated, offer.received, order.status_changed
	Channel   string      `json:"channel"`
	Timestamp time.Time   `json:"timestamp"`
	Payload   interface{} `json:"payload"`
}

// TrackingUpdate represents a real-time courier location broadcast to tracking clients
type TrackingUpdate struct {
	OrderID   *uuid.UUID `json:"order_id,omitempty"`
	CourierID uuid.UUID  `json:"courier_id"`
	Latitude  float64    `json:"latitude"`
	Longitude float64    `json:"longitude"`
	Heading   *float32   `json:"heading,omitempty"`
	SpeedKmh  *float32   `json:"speed_kmh,omitempty"`
	Timestamp time.Time  `json:"timestamp"`
}

// OfferNotification represents real-time dispatch offer sent to a courier over WebSocket
type OfferNotification struct {
	OfferID         uuid.UUID `json:"offer_id"`
	OrderID         uuid.UUID `json:"order_id"`
	PickupAddress   string    `json:"pickup_address"`
	PickupLatitude  float64   `json:"pickup_latitude"`
	PickupLongitude float64   `json:"pickup_longitude"`
	ExpiresAt       time.Time `json:"expires_at"`
	TTLSeconds      int       `json:"ttl_seconds"`
}
