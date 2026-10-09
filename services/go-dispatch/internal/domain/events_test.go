package domain

import (
	"encoding/json"
	"testing"

	"github.com/google/uuid"
)

func TestOrderCreatedEvent_InteropDeserialization(t *testing.T) {
	// Sample payload produced by ASP.NET Core OrderService.cs
	dotnetPayload := `{
		"orderId": "04f066c7-e7a3-4642-888a-0e2496d5d351",
		"orderNo": "ORD-20261009132352-7600",
		"customerId": "893c52e1-45a7-4773-a61b-90f757f12e12",
		"priority": 1,
		"vehicleType": "Car",
		"pickupLatitude": 40.7128,
		"pickupLongitude": -74.0060,
		"pickupAddress": "100 Main St, New York, NY",
		"dropoffLatitude": 40.7589,
		"dropoffLongitude": -73.9851,
		"dropoffAddress": "200 Market St, New York, NY",
		"packageDescription": "Express Documents",
		"packageWeight": 1.5,
		"requestedPickupAt": "2026-10-09T14:00:00Z",
		"createdAt": "2026-10-09T13:23:52.1234567Z"
	}`

	var event OrderCreatedEvent
	if err := json.Unmarshal([]byte(dotnetPayload), &event); err != nil {
		t.Fatalf("failed to unmarshal ASP.NET order.created payload: %v", err)
	}

	expectedOrderID, _ := uuid.Parse("04f066c7-e7a3-4642-888a-0e2496d5d351")
	if event.OrderID != expectedOrderID {
		t.Errorf("expected OrderID %v, got %v", expectedOrderID, event.OrderID)
	}

	if event.OrderNo != "ORD-20261009132352-7600" {
		t.Errorf("expected OrderNo ORD-20261009132352-7600, got %s", event.OrderNo)
	}

	if event.Priority != 1 {
		t.Errorf("expected Priority 1, got %d", event.Priority)
	}

	if event.VehicleType == nil || *event.VehicleType != "Car" {
		t.Errorf("expected VehicleType 'Car', got %v", event.VehicleType)
	}

	if event.PickupLatitude != 40.7128 || event.PickupLongitude != -74.0060 {
		t.Errorf("coordinate mismatch: got (%f, %f)", event.PickupLatitude, event.PickupLongitude)
	}

	if event.PackageWeight == nil || *event.PackageWeight != 1.5 {
		t.Errorf("expected PackageWeight 1.5, got %v", event.PackageWeight)
	}

	if event.CreatedAt.IsZero() {
		t.Error("expected non-zero CreatedAt time")
	}
}

func TestOrderCancelledEvent_InteropDeserialization(t *testing.T) {
	dotnetPayload := `{
		"orderId": "04f066c7-e7a3-4642-888a-0e2496d5d351",
		"previousStatus": "Created",
		"cancelledBy": "893c52e1-45a7-4773-a61b-90f757f12e12",
		"actorType": "Customer",
		"reason": "Customer cancelled before courier assignment",
		"occurredAt": "2026-10-09T13:24:00Z"
	}`

	var event OrderCancelledEvent
	if err := json.Unmarshal([]byte(dotnetPayload), &event); err != nil {
		t.Fatalf("failed to unmarshal ASP.NET order.cancelled payload: %v", err)
	}

	if event.PreviousStatus != "Created" {
		t.Errorf("expected previousStatus 'Created', got %s", event.PreviousStatus)
	}

	if event.Reason == nil || *event.Reason != "Customer cancelled before courier assignment" {
		t.Errorf("expected reason text, got %v", event.Reason)
	}
}

func TestCourierStatusChangedEvent_InteropDeserialization(t *testing.T) {
	dotnetPayload := `{
		"courierId": "d2665c51-bef5-46f5-b937-41f0ebd21c94",
		"fromStatus": "Offline",
		"toStatus": "Available",
		"vehicleType": "Car",
		"updatedAt": "2026-10-09T13:25:00Z"
	}`

	var event CourierStatusChangedEvent
	if err := json.Unmarshal([]byte(dotnetPayload), &event); err != nil {
		t.Fatalf("failed to unmarshal ASP.NET courier.status.changed payload: %v", err)
	}

	expectedCourierID, _ := uuid.Parse("d2665c51-bef5-46f5-b937-41f0ebd21c94")
	if event.CourierID != expectedCourierID {
		t.Errorf("expected CourierID %v, got %v", expectedCourierID, event.CourierID)
	}

	if event.ToStatus != "Available" || event.VehicleType != "Car" {
		t.Errorf("expected Available / Car, got %s / %s", event.ToStatus, event.VehicleType)
	}
}
