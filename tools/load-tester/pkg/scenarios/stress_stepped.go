package scenarios

import (
	"context"
	"fmt"
	"sync"
	"time"

	"load-tester/pkg/client"
	"load-tester/pkg/config"
	"load-tester/pkg/metrics"
	"load-tester/pkg/monitor"
)

// RunSteppedStress ramps concurrent users in steps to discover the exact breaking point.
func RunSteppedStress(ctx context.Context, cfg *config.Config, target *client.TargetClient, watchdog *monitor.Watchdog, col *metrics.Collector) {
	fmt.Println("\n================================================================================")
	fmt.Printf("   STEPPED STRESS TEST: BREAKING POINT DISCOVERY (Max VUs: %d)\n", cfg.MaxVUs)
	fmt.Println("================================================================================")

	currentVUs := cfg.VUs
	if currentVUs <= 0 {
		currentVUs = 50
	}

	stepNum := 1

	for currentVUs <= cfg.MaxVUs {
		select {
		case <-ctx.Done():
			return
		default:
		}

		fmt.Printf("\n▶ [STEP %d] Scaling to %d Concurrent Virtual Users (Duration: %v)...\n", stepNum, currentVUs, cfg.StepDuration)

		stepDeadline := time.Now().Add(cfg.StepDuration)
		var stepWg sync.WaitGroup

		col.SetActiveVUs(currentVUs)

		// Split traffic: 60% Customer journeys, 30% Courier telematics/radar, 10% Auth/Discovery
		customerVUs := int(float64(currentVUs) * 0.60)
		courierVUs := int(float64(currentVUs) * 0.30)
		if customerVUs == 0 { customerVUs = 1 }
		if courierVUs == 0 { courierVUs = 1 }
		directVUs := currentVUs - customerVUs - courierVUs
		if directVUs < 0 { directVUs = 0 }

		stepWg.Add(1)
		go func() {
			defer stepWg.Done()
			RunCustomerJourney(ctx, cfg, target, col, customerVUs, cfg.StepDuration)
		}()

		stepWg.Add(1)
		go func() {
			defer stepWg.Done()
			RunCourierTelematics(ctx, cfg, target, col, courierVUs, cfg.StepDuration)
		}()

		if directVUs > 0 {
			stepWg.Add(1)
			go func() {
				defer stepWg.Done()
				runDiscoveryPings(ctx, target, col, directVUs, stepDeadline)
			}()
		}

		stepWg.Wait()

		healthSnap := watchdog.GetStatusSnapshot()
		col.RecordStep(currentVUs, healthSnap)

		_, _, failed, stepRPS, p95 := col.CurrentStats()
		fmt.Printf("✔ [STEP %d COMPLETE] VUs: %d | Throughput: %.1f RPS | P95: %v | Errors: %d | Health: %s\n",
			stepNum, currentVUs, stepRPS, p95, failed, healthSnap)

		// Check if error rate is critical (>50%) to avoid frying infrastructure
		summary := col.GenerateSummary()
		if summary.ErrorRate >= 50.0 {
			fmt.Printf("\n🛑 CRITICAL THRESHOLD REACHED: Error rate exceeded 50.0%% (%.2f%%) at %d VUs.\nStopping ramp to prevent cascade failure.\n", summary.ErrorRate, currentVUs)
			break
		}

		currentVUs += cfg.StepVUs
		stepNum++
	}

	col.SetActiveVUs(0)
	fmt.Println("\n================================================================================")
	fmt.Println("   STEPPED STRESS TEST FINISHED")
	fmt.Println("================================================================================")
}

func runDiscoveryPings(ctx context.Context, target *client.TargetClient, col *metrics.Collector, numVUs int, deadline time.Time) {
	var wg sync.WaitGroup
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
				res, _ := target.Do(ctx, "GET", "/health", nil, nil, "")
				col.Record(res)
				time.Sleep(20 * time.Millisecond)
			}
		}()
	}
	wg.Wait()
}

