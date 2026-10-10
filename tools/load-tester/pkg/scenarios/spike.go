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

// RunSpike shoots an instantaneous burst of concurrent requests to test shock recovery.
func RunSpike(ctx context.Context, cfg *config.Config, target *client.TargetClient, col *metrics.Collector, spikeVUs int, numSpikes int) {
	fmt.Println("\n================================================================================")
	fmt.Printf("   SPIKE TEST: TRAFFIC SURGE SHOCK (%d Concurrent VUs, %d Spikes)\n", spikeVUs, numSpikes)
	fmt.Println("================================================================================")

	token, _, err := target.Login(ctx, cfg.AdminEmail, cfg.AdminPassword)
	if err != nil || token == "" {
		fmt.Printf("[-] Spike test could not authenticate admin: %v\n", err)
		return
	}

	for s := 1; s <= numSpikes; s++ {
		fmt.Printf("\n⚡ [SPIKE %d/%d] Triggering instantaneous burst of %d requests...\n", s, numSpikes, spikeVUs)

		startBarrier := make(chan struct{})
		var wg sync.WaitGroup

		col.SetActiveVUs(spikeVUs)

		for i := 0; i < spikeVUs; i++ {
			wg.Add(1)
			go func() {
				defer wg.Done()
				<-startBarrier // wait for synchronized blast
				res, _ := target.Do(ctx, "GET", "/api/orders", nil, nil, token)
				col.Record(res)
			}()
		}

		close(startBarrier) // release all requests simultaneously
		wg.Wait()

		col.SetActiveVUs(0)
		fmt.Printf("✔ [SPIKE %d/%d COMPLETE] Burst processed. Pausing 5s for cooldown...\n", s, numSpikes)
		time.Sleep(5 * time.Second)
	}

	fmt.Println("\n================================================================================")
	fmt.Println("   SPIKE TEST FINISHED")
	fmt.Println("================================================================================")
}

