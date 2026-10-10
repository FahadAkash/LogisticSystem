package reporter

import (
	"fmt"
	"strings"
	"time"

	"load-tester/pkg/metrics"
	"load-tester/pkg/monitor"
)

// ConsoleReporter displays a real-time terminal dashboard of test execution.
type ConsoleReporter struct {
	collector *metrics.Collector
	watchdog  *monitor.Watchdog
	stopChan  chan struct{}
}

// NewConsoleReporter creates an initialized console reporter.
func NewConsoleReporter(col *metrics.Collector, watchdog *monitor.Watchdog) *ConsoleReporter {
	return &ConsoleReporter{
		collector: col,
		watchdog:  watchdog,
		stopChan:  make(chan struct{}),
	}
}

// Start begins the live terminal dashboard updating at 2 Hz.
func (r *ConsoleReporter) Start() {
	go func() {
		ticker := time.NewTicker(500 * time.Millisecond)
		defer ticker.Stop()

		for {
			select {
			case <-r.stopChan:
				return
			case <-ticker.C:
				r.renderLiveStats()
			}
		}
	}()
}

// Stop stops the live terminal dashboard.
func (r *ConsoleReporter) Stop() {
	close(r.stopChan)
}

func (r *ConsoleReporter) renderLiveStats() {
	total, success, failed, rps, p95 := r.collector.CurrentStats()
	activeVUs := r.collector.GetActiveVUs()
	if total == 0 && activeVUs == 0 {
		return
	}

	errRate := 0.0
	if total > 0 {
		errRate = (float64(failed) / float64(total)) * 100.0
	}

	healthStatus := "PROBING"
	if r.watchdog != nil {
		healthStatus = r.watchdog.GetStatusSnapshot()
	}

	// Single-line or compact update to avoid terminal spam
	fmt.Printf("\r⏳ [VUs: %4d] | RPS: %7.1f | Total: %6d | OK: %6d | Err: %4d (%.1f%%) | P95: %7v | Health: %-20s",
		activeVUs, rps, total, success, failed, errRate, p95, healthStatus)
}

// PrintFinalSummary renders the final comprehensive report to stdout.
func (r *ConsoleReporter) PrintFinalSummary(report metrics.SummaryReport) {
	fmt.Println("\n\n================================================================================")
	fmt.Println("                 LOAD & STRESS TEST FINAL SUMMARY REPORT                        ")
	fmt.Println("================================================================================")
	fmt.Printf(" Target URL      : %s\n", report.TargetURL)
	fmt.Printf(" Test Mode       : %s\n", strings.ToUpper(report.Mode))
	fmt.Printf(" Duration        : %v (%s to %s)\n", report.TotalDuration.Round(time.Millisecond), report.StartTime.Format("15:04:05"), report.EndTime.Format("15:04:05"))
	fmt.Printf(" Total Requests  : %d\n", report.TotalRequests)
	fmt.Printf(" Successful (2xx): %d (%.2f%%)\n", report.SuccessfulReqs, 100.0-report.ErrorRate)
	fmt.Printf(" Failed / Errored: %d (%.2f%%)\n", report.FailedReqs, report.ErrorRate)
	fmt.Printf(" Overall RPS     : %.2f req/sec\n", report.OverallRPS)
	fmt.Printf(" Peak RPS        : %.2f req/sec\n", report.PeakRPS)
	fmt.Printf(" Peak VUs Tested : %d concurrent users\n", report.PeakVUs)

	if report.BreakingPointVU > 0 {
		fmt.Printf(" Breaking Point  : ⚠️ DETECTED AT ~%d CONCURRENT USERS (Error rate crossed 5%%)\n", report.BreakingPointVU)
	} else {
		fmt.Println(" Breaking Point  : ✅ NONE REACHED (System remained fully stable under tested load)")
	}

	fmt.Println("--------------------------------------------------------------------------------")
	fmt.Println(" LATENCY DISTRIBUTION:")
	fmt.Printf("   Min Latency   : %v\n", report.MinLatency.Round(time.Microsecond))
	fmt.Printf("   Avg Latency   : %v\n", report.AvgLatency.Round(time.Microsecond))
	fmt.Printf("   P50 (Median)  : %v\n", report.P50Latency.Round(time.Microsecond))
	fmt.Printf("   P90 Latency   : %v\n", report.P90Latency.Round(time.Microsecond))
	fmt.Printf("   P95 Latency   : %v\n", report.P95Latency.Round(time.Microsecond))
	fmt.Printf("   P99 Latency   : %v\n", report.P99Latency.Round(time.Microsecond))
	fmt.Printf("   Max Latency   : %v\n", report.MaxLatency.Round(time.Microsecond))

	if len(report.StatusCodes) > 0 {
		fmt.Println("--------------------------------------------------------------------------------")
		fmt.Println(" HTTP STATUS CODES:")
		for code, count := range report.StatusCodes {
			label := fmt.Sprintf("HTTP %d", code)
			if code == 0 {
				label = "Network/Timeout"
			}
			fmt.Printf("   %-16s : %d requests\n", label, count)
		}
	}

	if len(report.ErrorBreakdown) > 0 {
		fmt.Println("--------------------------------------------------------------------------------")
		fmt.Println(" ERROR ROOT-CAUSE BREAKDOWN:")
		for errType, count := range report.ErrorBreakdown {
			fmt.Printf("   %-24s : %d occurrences\n", errType, count)
		}
	}

	if r.watchdog != nil {
		failures := r.watchdog.GetFailureLog()
		if len(failures) > 0 {
			fmt.Println("--------------------------------------------------------------------------------")
			fmt.Println(" SERVICE CRASH & OUTAGE TIMELINE:")
			for _, f := range failures {
				fmt.Printf("   ⚠️  %s\n", f)
			}
		}
	}

	fmt.Println("--------------------------------------------------------------------------------")
	fmt.Println(" DIAGNOSIS & CAPACITY ASSESSMENT:")
	fmt.Printf("   %s\n", report.Diagnosis)
	fmt.Println("================================================================================\n")
}

