package domain

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
	"go-dispatch/internal/config"
)

func getTestRedis(t *testing.T) *redis.Client {
	t.Helper()
	cfg := config.Load("8082")
	addr := fmt.Sprintf("%s:%s", cfg.RedisHost, cfg.RedisPort)

	rdb := redis.NewClient(&redis.Options{
		Addr:     addr,
		Password: cfg.RedisPassword,
		DB:       0,
	})

	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()

	if err := rdb.Ping(ctx).Err(); err != nil {
		t.Skipf("Redis not reachable at %s, skipping live test: %v", addr, err)
		return nil
	}

	return rdb
}

func TestRedisCourierGeoAndState_AspNetCoreInterop(t *testing.T) {
	rdb := getTestRedis(t)
	defer rdb.Close()

	ctx := context.Background()
	courierID := uuid.New().String()

	// 1. Simulate ASP.NET Core CourierService.cs writing to Redis:
	// Coordinates: New York City Hall (Longitude: -74.0060, Latitude: 40.7128)
	lon := -74.0060
	lat := 40.7128
	now := time.Now().UTC().Format(time.RFC3339)

	// In C#: await db.GeoAddAsync("couriers:geo", longitude, latitude, courier.Id.ToString());
	err := rdb.GeoAdd(ctx, "couriers:geo", &redis.GeoLocation{
		Name:      courierID,
		Longitude: lon,
		Latitude:  lat,
	}).Err()
	if err != nil {
		t.Fatalf("failed to GeoAdd courier: %v", err)
	}
	defer rdb.ZRem(ctx, "couriers:geo", courierID)

	// In C#: await db.HashSetAsync($"courier:{courier.Id}:state", new HashEntry[] { ... });
	stateKey := fmt.Sprintf("courier:%s:state", courierID)
	err = rdb.HSet(ctx, stateKey, map[string]interface{}{
		"status":       "Available",
		"vehicle_type": "Car",
		"updated_at":   now,
	}).Err()
	if err != nil {
		t.Fatalf("failed to HSet courier state: %v", err)
	}
	defer rdb.Del(ctx, stateKey)

	// 2. In Go: Dispatch engine searches for candidate couriers within 2km of pickup location
	// Pickup point: Wall Street (Longitude: -74.0088, Latitude: 40.7075) - approx 600m away
	pickupLon := -74.0088
	pickupLat := 40.7075

	locations, err := rdb.GeoSearchLocation(ctx, "couriers:geo", &redis.GeoSearchLocationQuery{
		GeoSearchQuery: redis.GeoSearchQuery{
			Longitude:  pickupLon,
			Latitude:   pickupLat,
			Radius:     2000,
			RadiusUnit: "m",
			Sort:       "ASC",
		},
		WithCoord: true,
		WithDist:  true,
	}).Result()
	if err != nil {
		t.Fatalf("GeoSearchLocation failed: %v", err)
	}

	// Verify courier is discovered
	found := false
	var courierDist float64
	for _, loc := range locations {
		if loc.Name == courierID {
			found = true
			courierDist = loc.Dist
			break
		}
	}

	if !found {
		t.Fatalf("expected courier %s to be discovered in 2000m radius of pickup, but was not found", courierID)
	}

	if courierDist <= 0 || courierDist > 2000 {
		t.Errorf("expected courier distance between 0 and 2000m, got %f", courierDist)
	}

	// 3. In Go: Read courier state hash written by ASP.NET
	stateMap, err := rdb.HGetAll(ctx, stateKey).Result()
	if err != nil {
		t.Fatalf("HGetAll failed: %v", err)
	}

	if stateMap["status"] != "Available" {
		t.Errorf("expected status 'Available', got %s", stateMap["status"])
	}
	if stateMap["vehicle_type"] != "Car" {
		t.Errorf("expected vehicle_type 'Car', got %s", stateMap["vehicle_type"])
	}

	// 4. Simulate ASP.NET Core Courier transitioning to Busy / Offline:
	// In C#: await db.SortedSetRemoveAsync("couriers:geo", courier.Id.ToString());
	err = rdb.ZRem(ctx, "couriers:geo", courierID).Err()
	if err != nil {
		t.Fatalf("ZRem failed: %v", err)
	}

	// Verify courier is no longer returned in GeoSearch
	afterLocations, err := rdb.GeoSearch(ctx, "couriers:geo", &redis.GeoSearchQuery{
		Longitude:  pickupLon,
		Latitude:   pickupLat,
		Radius:     2000,
		RadiusUnit: "m",
	}).Result()
	if err != nil {
		t.Fatalf("GeoSearch after status change failed: %v", err)
	}

	for _, name := range afterLocations {
		if name == courierID {
			t.Fatalf("courier %s should NOT appear in geospatial search when Offline or Busy", courierID)
		}
	}
}
