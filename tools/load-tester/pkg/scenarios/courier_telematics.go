package scenarios

import (
	"context"
	"encoding/json"
	"fmt"
	"math/rand"
	"sync"
	"time"

	"github.com/google/uuid"
	"load-tester/pkg/client"
	"load-tester/pkg/config"
	"load-tester/pkg/metrics"
)

// RunCourierTelematics floods Go Ingest and API with courier GPS telemetry and status toggles.
func RunCourierTelematics(ctx context.Context, cfg *config.Config, target *client.TargetClient, col *metrics.Collector, numVUs int, duration time.Duration) {
	var wg sync.WaitGroup
	deadline := time.Now().Add(duration)

	col.SetActiveVUs(numVUs)
	defer col.SetActiveVUs(0)

	token, _, err := target.Login(ctx, cfg.AdminEmail, cfg.AdminPassword)
	if err != nil || token == "" {
		fmt.Printf("[-] Courier telematics failed to authenticate admin: %v\n", err)
		return
	}

	ingestClient := client.NewTargetClient(cfg.GoIngestURL, 5*time.Second)

	for i := 0; i < numVUs; i++ {
		wg.Add(1)
		go func(workerID int) {
			defer wg.Done()

			courierID := uuid.New()
			r := rand.New(rand.NewSource(time.Now().UnixNano() + int64(workerID)))

			baseLat := 40.7128 + (r.Float64() * 0.05)
			baseLon := -74.0060 + (r.Float64() * 0.05)

			for time.Now().Before(deadline) {
				select {
				case <-ctx.Done():
					return
				default:
				}

				// 1. Send GPS Location Ping to Go Ingest
				baseLat += (r.Float64() - 0.5) * 0.001
				baseLon += (r.Float64() - 0.5) * 0.001

				pingBody, _ := json.Marshal(map[string]interface{}{
					"courierId": courierID,
					"latitude":  baseLat,
					"longitude": baseLon,
					"speed":     25.5 + r.Float64()*10.0,
					"heading":   r.Float64() * 360.0,
					"timestamp": time.Now().UTC(),
				})

				res, _ := ingestClient.Do(ctx, "POST", "/api/v1/location", pingBody, nil, "")
				col.Record(res)

				// 2. Occasionally poll fleet radar (every 20th iteration)
				if r.Intn(20) == 0 {
					radarRes, _ := target.Do(ctx, "GET", "/api/couriers", nil, nil, token)
					col.Record(radarRes)
				}

				// Realistic GPS interval (10ms to 50ms per simulated courier)
				time.Sleep(time.Duration(10+r.Intn(40)) * time.Millisecond)
			}
		}(i)
	}

	wg.Wait()
}

