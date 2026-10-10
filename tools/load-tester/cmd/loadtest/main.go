package main

import (
	"context"
	"fmt"
	"os"
	"os/signal"
	"syscall"
	"time"

	"load-tester/pkg/client"
	"load-tester/pkg/config"
	"load-tester/pkg/metrics"
	"load-tester/pkg/monitor"
	"load-tester/pkg/reporter"
	"load-tester/pkg/scenarios"
)

func main() {
	cfg := config.ParseFlags()

	fmt.Println("================================================================================")
	fmt.Println("   DISTRIBUTED LOGISTICS PLATFORM - LOAD & STRESS TESTING ENGINE                ")
	fmt.Println("================================================================================")
	fmt.Printf(" Target Base URL : %s\n", cfg.TargetURL)
	fmt.Printf(" Go Ingest URL   : %s\n", cfg.GoIngestURL)
	fmt.Printf(" Go Gateway URL  : %s\n", cfg.GoGatewayURL)
	fmt.Printf(" Scenario Mode   : %s\n", cfg.Mode)
	fmt.Printf(" Default VUs     : %d\n", cfg.VUs)
	fmt.Printf(" Max VUs (Stress): %d\n", cfg.MaxVUs)
	fmt.Printf(" Duration / Step : %v / %v\n", cfg.Duration, cfg.StepDuration)
	fmt.Println("================================================================================")

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// Handle graceful termination on Ctrl+C
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, os.Interrupt, syscall.SIGTERM)
	go func() {
		<-sigChan
		fmt.Println("\n⚠️  Termination signal received. Wrapping up active requests and generating report...")
		cancel()
	}()

	targetClient := client.NewTargetClient(cfg.TargetURL, cfg.Timeout)
	wsClient := client.NewWebSocketClient(targetClient, cfg.GoGatewayURL)
	collector := metrics.NewCollector(cfg.TargetURL, cfg.Mode)

	// Start health watchdog
	watchdog := monitor.NewWatchdog(cfg.TargetURL, cfg.GoIngestURL, cfg.GoGatewayURL, collector.GetActiveVUs)
	watchdog.Start()
	defer watchdog.Stop()

	// Start live console reporter
	consoleRep := reporter.NewConsoleReporter(collector, watchdog)
	if cfg.Mode != "smoke" {
		consoleRep.Start()
	}

	startTime := time.Now()

	// Execute selected scenario
	switch cfg.Mode {
	case "smoke":
		if err := scenarios.RunSmoke(ctx, cfg, targetClient, wsClient, collector); err != nil {
			fmt.Printf("[-] Smoke test failed: %v\n", err)
		}

	case "load", "journey":
		fmt.Printf("\n▶ Starting fixed load test with %d VUs for %v...\n", cfg.VUs, cfg.Duration)
		scenarios.RunCustomerJourney(ctx, cfg, targetClient, collector, cfg.VUs, cfg.Duration)

	case "courier", "telematics":
		fmt.Printf("\n▶ Starting courier GPS telematics flood with %d VUs for %v...\n", cfg.VUs, cfg.Duration)
		scenarios.RunCourierTelematics(ctx, cfg, targetClient, collector, cfg.VUs, cfg.Duration)

	case "ws", "websocket":
		fmt.Printf("\n▶ Starting WebSocket concurrency stress test with %d VUs for %v...\n", cfg.VUs, cfg.Duration)
		scenarios.RunWebSocketStress(ctx, cfg, targetClient, wsClient, collector, cfg.VUs, cfg.Duration)

	case "stress":
		scenarios.RunSteppedStress(ctx, cfg, targetClient, watchdog, collector)

	case "spike":
		scenarios.RunSpike(ctx, cfg, targetClient, collector, cfg.VUs, 3)

	case "all":
		fmt.Println("\n--- PHASE 1: Smoke Sanity Verification ---")
		_ = scenarios.RunSmoke(ctx, cfg, targetClient, wsClient, collector)

		fmt.Println("\n--- PHASE 2: Stepped Stress & Breaking Point Discovery ---")
		scenarios.RunSteppedStress(ctx, cfg, targetClient, watchdog, collector)

		fmt.Println("\n--- PHASE 3: Sudden Traffic Spike Shock ---")
		scenarios.RunSpike(ctx, cfg, targetClient, collector, cfg.VUs, 2)

	default:
		fmt.Printf("[-] Unknown mode: %s. Available modes: smoke, load, stress, spike, ws, journey, all\n", cfg.Mode)
		return
	}

	if cfg.Mode != "smoke" {
		consoleRep.Stop()
	}

	_ = startTime

	// Generate and output summary reports
	summary := collector.GenerateSummary()
	consoleRep.PrintFinalSummary(summary)

	if cfg.ReportHTML != "" {
		if err := reporter.GenerateHTMLReport(cfg.ReportHTML, summary, watchdog); err != nil {
			fmt.Printf("[-] Failed to write HTML report: %v\n", err)
		} else {
			fmt.Printf("📄 Interactive HTML Report saved to: %s\n", cfg.ReportHTML)
		}
	}

	if cfg.ReportMD != "" {
		if err := reporter.GenerateMarkdownReport(cfg.ReportMD, summary, watchdog); err != nil {
			fmt.Printf("[-] Failed to write Markdown report: %v\n", err)
		} else {
			fmt.Printf("📝 Markdown Summary Report saved to: %s\n", cfg.ReportMD)
		}
	}
}

