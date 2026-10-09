package domain

import (
	"time"

	"github.com/google/uuid"
)

// DispatchOrder represents a row in dispatch.dispatch_orders
type DispatchOrder struct {
	OrderID           uuid.UUID  `json:"order_id" db:"order_id"`
	State             string     `json:"state" db:"state"` // Searching, Offered, Assigned, Cancelled, Unassigned, Completed
	PickupLongitude   float64    `json:"pickup_longitude"`
	PickupLatitude    float64    `json:"pickup_latitude"`
	VehicleType       *string    `json:"vehicle_type,omitempty" db:"vehicle_type"`
	Priority          int16      `json:"priority" db:"priority"`
	AttemptCount      int        `json:"attempt_count" db:"attempt_count"`
	SearchRadiusM     int        `json:"search_radius_m" db:"search_radius_m"`
	CurrentOfferID    *uuid.UUID `json:"current_offer_id,omitempty" db:"current_offer_id"`
	AssignedCourierID *uuid.UUID `json:"assigned_courier_id,omitempty" db:"assigned_courier_id"`
	Version           int        `json:"version" db:"version"` // Optimistic concurrency
	CreatedAt         time.Time  `json:"created_at" db:"created_at"`
	UpdatedAt         time.Time  `json:"updated_at" db:"updated_at"`
}

// Offer represents a row in dispatch.offers
type Offer struct {
	ID          uuid.UUID  `json:"id" db:"id"`
	OrderID     uuid.UUID  `json:"order_id" db:"order_id"`
	CourierID   uuid.UUID  `json:"courier_id" db:"courier_id"`
	AttemptNo   int        `json:"attempt_no" db:"attempt_no"`
	Status      string     `json:"status" db:"status"` // Pending, Accepted, Rejected, Expired, Cancelled
	Score       *float64   `json:"score,omitempty" db:"score"`
	OfferedAt   time.Time  `json:"offered_at" db:"offered_at"`
	ExpiresAt   time.Time  `json:"expires_at" db:"expires_at"`
	RespondedAt *time.Time `json:"responded_at,omitempty" db:"responded_at"`
}

// DispatchTransition represents a row in dispatch.dispatch_transitions
type DispatchTransition struct {
	ID         int64     `json:"id" db:"id"`
	OrderID    uuid.UUID `json:"order_id" db:"order_id"`
	FromState  *string   `json:"from_state,omitempty" db:"from_state"`
	ToState    string    `json:"to_state" db:"to_state"`
	Trigger    string    `json:"trigger" db:"trigger"`
	OccurredAt time.Time `json:"occurred_at" db:"occurred_at"`
}

// CourierProfile represents a row in dispatch.courier_profiles (local read model fed by courier.events)
type CourierProfile struct {
	CourierID   uuid.UUID `json:"courier_id" db:"courier_id"`
	Status      string    `json:"status" db:"status"`
	VehicleType *string   `json:"vehicle_type,omitempty" db:"vehicle_type"`
	Rating      float64   `json:"rating" db:"rating"`
	UpdatedAt   time.Time `json:"updated_at" db:"updated_at"`
}

// DispatchConfig represents a row in dispatch.dispatch_config
type DispatchConfig struct {
	Key       string    `json:"key" db:"key"`
	Value     string    `json:"value" db:"value"`
	UpdatedAt time.Time `json:"updated_at" db:"updated_at"`
}

// OutboxMessage represents a row in dispatch.outbox_messages
type OutboxMessage struct {
	ID            uuid.UUID  `json:"id" db:"id"`
	AggregateType string     `json:"aggregate_type" db:"aggregate_type"`
	AggregateID   uuid.UUID  `json:"aggregate_id" db:"aggregate_id"`
	Topic         string     `json:"topic" db:"topic"`
	MessageKey    string     `json:"message_key" db:"message_key"`
	EventType     string     `json:"event_type" db:"event_type"`
	EventVersion  int        `json:"event_version" db:"event_version"`
	Payload       []byte     `json:"payload" db:"payload"`
	Headers       []byte     `json:"headers" db:"headers"`
	CreatedAt     time.Time  `json:"created_at" db:"created_at"`
	PublishedAt   *time.Time `json:"published_at,omitempty" db:"published_at"`
}

// ProcessedEvent represents a row in dispatch.processed_events
type ProcessedEvent struct {
	EventID      uuid.UUID `json:"event_id" db:"event_id"`
	ConsumerName string    `json:"consumer_name" db:"consumer_name"`
	ProcessedAt  time.Time `json:"processed_at" db:"processed_at"`
}

