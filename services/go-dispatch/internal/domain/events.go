package domain

import (
	"time"

	"github.com/google/uuid"
)

// OrderCreatedEvent represents Kafka topic order.events event_type order.created
type OrderCreatedEvent struct {
	OrderID            uuid.UUID  `json:"orderId"`
	OrderNo            string     `json:"orderNo"`
	CustomerID         uuid.UUID  `json:"customerId"`
	Priority           int        `json:"priority"`
	VehicleType        *string    `json:"vehicleType,omitempty"`
	PickupLatitude     float64    `json:"pickupLatitude"`
	PickupLongitude    float64    `json:"pickupLongitude"`
	PickupAddress      string     `json:"pickupAddress"`
	DropoffLatitude    float64    `json:"dropoffLatitude"`
	DropoffLongitude   float64    `json:"dropoffLongitude"`
	DropoffAddress     string     `json:"dropoffAddress"`
	PackageDescription *string    `json:"packageDescription,omitempty"`
	PackageWeight      *float64   `json:"packageWeight,omitempty"`
	RequestedPickupAt  *time.Time `json:"requestedPickupAt,omitempty"`
	CreatedAt          time.Time  `json:"createdAt"`
}

// OrderCancelledEvent represents Kafka topic order.events event_type order.cancelled
type OrderCancelledEvent struct {
	OrderID        uuid.UUID `json:"orderId"`
	PreviousStatus string    `json:"previousStatus"`
	CancelledBy    uuid.UUID `json:"cancelledBy"`
	ActorType      string    `json:"actorType"`
	Reason         *string   `json:"reason,omitempty"`
	OccurredAt     time.Time `json:"occurredAt"`
}

// OrderStatusChangedEvent represents Kafka topic order.events event_type order.status.changed
type OrderStatusChangedEvent struct {
	OrderID        uuid.UUID  `json:"orderId"`
	PreviousStatus string     `json:"previousStatus"`
	NewStatus      string     `json:"newStatus"`
	CourierID      *uuid.UUID `json:"courierId,omitempty"`
	Reason         *string    `json:"reason,omitempty"`
	OccurredAt     time.Time  `json:"occurredAt"`
}

// CourierStatusChangedEvent represents Kafka topic courier.events event_type courier.status.changed
type CourierStatusChangedEvent struct {
	CourierID   uuid.UUID `json:"courierId"`
	FromStatus  string    `json:"fromStatus"`
	ToStatus    string    `json:"toStatus"`
	VehicleType string    `json:"vehicleType"`
	UpdatedAt   time.Time `json:"updatedAt"`
}
