package domain

import (
	"time"

	"github.com/google/uuid"
)

// LocationPing represents an incoming courier location ping over HTTP
type LocationPing struct {
	CourierID uuid.UUID `json:"courier_id"`
	Latitude  float64   `json:"latitude"`
	Longitude float64   `json:"longitude"`
	SpeedKmh  *float32  `json:"speed_kmh,omitempty"`
	Heading   *float32  `json:"heading,omitempty"`
	AccuracyM *float32  `json:"accuracy_m,omitempty"`
	OrderID   *uuid.UUID `json:"order_id,omitempty"`
	Timestamp time.Time `json:"timestamp"`
}

// CourierLocationHistory represents a row in tracking.courier_location_history
type CourierLocationHistory struct {
	CourierID  uuid.UUID  `json:"courier_id" db:"courier_id"`
	RecordedAt time.Time  `json:"recorded_at" db:"recorded_at"`
	Latitude   float64    `json:"latitude"`
	Longitude  float64    `json:"longitude"`
	SpeedKmh   *float32   `json:"speed_kmh,omitempty" db:"speed_kmh"`
	Heading    *float32   `json:"heading,omitempty" db:"heading"`
	AccuracyM  *float32   `json:"accuracy_m,omitempty" db:"accuracy_m"`
	OrderID    *uuid.UUID `json:"order_id,omitempty" db:"order_id"`
}

// CourierLastLocation represents a row in tracking.courier_last_location
type CourierLastLocation struct {
	CourierID  uuid.UUID `json:"courier_id" db:"courier_id"`
	Latitude   float64   `json:"latitude"`
	Longitude  float64   `json:"longitude"`
	RecordedAt time.Time `json:"recorded_at" db:"recorded_at"`
	Status     *string   `json:"status,omitempty" db:"status"`
	UpdatedAt  time.Time `json:"updated_at" db:"updated_at"`
}

// ProcessedEvent represents a row in tracking.processed_events
type ProcessedEvent struct {
	EventID      uuid.UUID `json:"event_id" db:"event_id"`
	ConsumerName string    `json:"consumer_name" db:"consumer_name"`
	ProcessedAt  time.Time `json:"processed_at" db:"processed_at"`
}

