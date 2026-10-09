package domain

import (
	"time"

	"github.com/google/uuid"
)

// ETARequest represents an ETA calculation request
type ETARequest struct {
	OrderID         uuid.UUID `json:"order_id"`
	OriginLat       float64   `json:"origin_lat"`
	OriginLng       float64   `json:"origin_lng"`
	DestinationLat  float64   `json:"destination_lat"`
	DestinationLng  float64   `json:"destination_lng"`
	VehicleType     string    `json:"vehicle_type"`
}

// ETAResponse represents an ETA calculation response
type ETAResponse struct {
	OrderID          uuid.UUID `json:"order_id"`
	DistanceMeters   float64   `json:"distance_meters"`
	DurationSeconds  int       `json:"duration_seconds"`
	EstimatedArrival time.Time `json:"estimated_arrival"`
}
