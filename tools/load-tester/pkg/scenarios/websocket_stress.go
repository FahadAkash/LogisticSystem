package scenarios

import (
	"context"
	"fmt"
	"sync"
	"time"

	"load-tester/pkg/client"
	"load-tester/pkg/config"
	"load-tester/pkg/metrics"
)

// RunWebSocketStress establishes hundreds/thousands of concurrent WebSockets to test gateway capacity.
func RunWebSocketStress(ctx context.Context, cfg *config.Config, target *client.TargetClient, wsClient *client.WebSocketClient, col *metrics.Collector, numVUs int, duration time.Duration) {
	var wg sync.WaitGroup
	deadline := time.Now().Add(duration)

	col.SetActiveVUs(numVUs)
	defer col.SetActiveVUs(0)

	token, _, err := target.Login(ctx, cfg.AdminEmail, cfg.AdminPassword)
	if err != nil || token == "" {
		fmt.Printf("[-] WebSocket stress failed to authenticate admin: %v\n", err)
		return
	}

	for i := 0; i < numVUs; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()

			for time.Now().Before(deadline) {
				select {
				case <-ctx.Done():
					return
				default:
				}

				// 1. Acquire single-use ticket
				ticket, res, err := wsClient.AcquireTicket(ctx, token)
				col.Record(res)
				if err != nil || ticket == "" {
					time.Sleep(500 * time.Millisecond)
					continue
				}

				// 2. Connect and ping
				wsRes := wsClient.ConnectAndPing(ctx, ticket)
				col.Record(wsRes)

				// Hold connection or reconnect interval
				time.Sleep(100 * time.Millisecond)
			}
		}()
	}

	wg.Wait()
}

