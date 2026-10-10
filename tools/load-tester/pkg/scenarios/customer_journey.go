package scenarios

import (
	"context"
	"encoding/json"
	"fmt"
	"math/rand"
	"net/http"
	"sync"
	"time"

	"github.com/google/uuid"
	"load-tester/pkg/client"
	"load-tester/pkg/config"
	"load-tester/pkg/metrics"
)

// RunCustomerJourney simulates realistic end-to-end customer operations concurrently.
func RunCustomerJourney(ctx context.Context, cfg *config.Config, target *client.TargetClient, col *metrics.Collector, numVUs int, duration time.Duration) {
	var wg sync.WaitGroup
	deadline := time.Now().Add(duration)

	col.SetActiveVUs(numVUs)
	defer col.SetActiveVUs(0)

	// Pre-login admin to create a shared pool or run independent customer accounts
	token, _, err := target.Login(ctx, cfg.AdminEmail, cfg.AdminPassword)
	if err != nil || token == "" {
		fmt.Printf("[-] Customer journey failed to authenticate admin: %v\n", err)
		return
	}

	for i := 0; i < numVUs; i++ {
		wg.Add(1)
		go func(workerID int) {
			defer wg.Done()

			r := rand.New(rand.NewSource(time.Now().UnixNano() + int64(workerID)))

			for time.Now().Before(deadline) {
				select {
				case <-ctx.Done():
					return
				default:
				}

				// 1. Create Order with Idempotency Key
				orderIdemKey := uuid.New().String()
				pickupLat := 40.7000 + (r.Float64() * 0.05)
				pickupLon := -74.0100 + (r.Float64() * 0.05)
				dropoffLat := 40.7200 + (r.Float64() * 0.05)
				dropoffLon := -73.9800 + (r.Float64() * 0.05)

				orderReq, _ := json.Marshal(map[string]interface{}{
					"vehicleType":        "Bike",
					"packageDescription": fmt.Sprintf("Stress Package #%d-%d", workerID, r.Intn(10000)),
					"packageWeight":      1.5 + r.Float64()*5.0,
					"stops": []map[string]interface{}{
						{
							"type":           "Pickup",
							"sequence":       1,
							"address":        fmt.Sprintf("%d Broad St", r.Intn(500)),
							"contactName":    "Sender Test",
							"contactPhone":   "+15551234567",
							"latitude":       pickupLat,
							"longitude":      pickupLon,
						},
						{
							"type":           "Dropoff",
							"sequence":       2,
							"address":        fmt.Sprintf("%d 5th Ave", r.Intn(500)),
							"contactName":    "Receiver Test",
							"contactPhone":   "+15559876543",
							"latitude":       dropoffLat,
							"longitude":      dropoffLon,
						},
					},

				})

				headers := map[string]string{
					"Idempotency-Key": orderIdemKey,
				}

				res, orderData := target.Do(ctx, "POST", "/api/orders", orderReq, headers, token)
				col.Record(res)

				if res.StatusCode == http.StatusCreated || res.StatusCode == http.StatusOK {
					var orderResp struct {
						ID uuid.UUID `json:"id"`
					}
					_ = json.Unmarshal(orderData, &orderResp)

					if orderResp.ID != uuid.Nil {
						// 2. Query order
						getRes, _ := target.Do(ctx, "GET", fmt.Sprintf("/api/orders/%s", orderResp.ID), nil, nil, token)
						col.Record(getRes)

						// 3. Occasionally cancel (10% chance)
						if r.Intn(10) == 0 {
							cancelBody, _ := json.Marshal(map[string]string{"reason": "Load test cancellation"})
							cancelRes, _ := target.Do(ctx, "POST", fmt.Sprintf("/api/orders/%s/cancel", orderResp.ID), cancelBody, nil, token)
							col.Record(cancelRes)
						}
					}
				}

				// Small pacing jitter between loops (5ms to 20ms)
				time.Sleep(time.Duration(5+r.Intn(15)) * time.Millisecond)
			}
		}(i)
	}

	wg.Wait()
}

