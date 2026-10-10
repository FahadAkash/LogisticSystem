package reporter

import (
	"fmt"
	"os"
	"strings"
	"time"

	"load-tester/pkg/metrics"
	"load-tester/pkg/monitor"
)

// GenerateMarkdownReport writes a formatted GitHub-ready markdown summary.
func GenerateMarkdownReport(filePath string, report metrics.SummaryReport, watchdog *monitor.Watchdog) error {
	var sb strings.Builder

	sb.WriteString("# Distributed Logistics Platform — Load & Stress Test Report\n\n")
	sb.WriteString(fmt.Sprintf("**Test Execution Timestamp**: `%s`  \n", report.EndTime.Format("2006-01-02 15:04:05 UTC")))
	sb.WriteString(fmt.Sprintf("**Target Endpoint**: `%s`  \n", report.TargetURL))
	sb.WriteString(fmt.Sprintf("**Scenario Mode**: `%s`  \n", strings.ToUpper(report.Mode)))
	sb.WriteString(fmt.Sprintf("**Total Duration**: `%v`  \n\n", report.TotalDuration.Round(time.Millisecond)))

	sb.WriteString("## 1. Executive Summary & Capacity Assessment\n\n")
	sb.WriteString(fmt.Sprintf("> **System Diagnosis**: %s\n\n", report.Diagnosis))

	breakingPoint := "✅ None Detected (System Remained Stable)"
	if report.BreakingPointVU > 0 {
		breakingPoint = fmt.Sprintf("⚠️ **~%d Concurrent Users** (Error rate crossed 5%%)", report.BreakingPointVU)
	}

	sb.WriteString("| Metric | Value |\n")
	sb.WriteString("|---|---|\n")
	sb.WriteString(fmt.Sprintf("| **Total Requests Processed** | %d |\n", report.TotalRequests))
	sb.WriteString(fmt.Sprintf("| **Successful Requests (2xx)** | %d (%.2f%%) |\n", report.SuccessfulReqs, 100.0-report.ErrorRate))
	sb.WriteString(fmt.Sprintf("| **Failed / Errored Requests** | %d (%.2f%%) |\n", report.FailedReqs, report.ErrorRate))
	sb.WriteString(fmt.Sprintf("| **Average Throughput (RPS)** | %.2f req/sec |\n", report.OverallRPS))
	sb.WriteString(fmt.Sprintf("| **Peak Throughput (RPS)** | %.2f req/sec |\n", report.PeakRPS))
	sb.WriteString(fmt.Sprintf("| **Peak Virtual Users Tested** | %d VUs |\n", report.PeakVUs))
	sb.WriteString(fmt.Sprintf("| **Breaking Point Threshold** | %s |\n\n", breakingPoint))

	sb.WriteString("## 2. Latency Distribution\n\n")
	sb.WriteString("| Percentile | Latency |\n")
	sb.WriteString("|---|---|\n")
	sb.WriteString(fmt.Sprintf("| **Min Latency** | %v |\n", report.MinLatency.Round(time.Microsecond)))
	sb.WriteString(fmt.Sprintf("| **P50 (Median)** | %v |\n", report.P50Latency.Round(time.Microsecond)))
	sb.WriteString(fmt.Sprintf("| **P90 Latency** | %v |\n", report.P90Latency.Round(time.Microsecond)))
	sb.WriteString(fmt.Sprintf("| **P95 Latency** | %v |\n", report.P95Latency.Round(time.Microsecond)))
	sb.WriteString(fmt.Sprintf("| **P99 Latency** | %v |\n", report.P99Latency.Round(time.Microsecond)))
	sb.WriteString(fmt.Sprintf("| **Max Latency** | %v |\n\n", report.MaxLatency.Round(time.Microsecond)))

	if len(report.Steps) > 0 {
		sb.WriteString("## 3. Stepped Concurrency Progression\n\n")
		sb.WriteString("| Concurrent VUs | Throughput (RPS) | Total Requests | Error Rate | P50 (ms) | P95 (ms) | P99 (ms) | Service Health |\n")
		sb.WriteString("|---|---|---|---|---|---|---|---|\n")
		for _, step := range report.Steps {
			p50ms := float64(step.P50.Microseconds()) / 1000.0
			p95ms := float64(step.P95.Microseconds()) / 1000.0
			p99ms := float64(step.P99.Microseconds()) / 1000.0
			sb.WriteString(fmt.Sprintf("| %d VUs | %.1f | %d | %.2f%% | %.1f ms | %.1f ms | %.1f ms | %s |\n",
				step.VUs, step.RPS, step.TotalRequests, step.ErrorRate, p50ms, p95ms, p99ms, step.HealthStatus))
		}
		sb.WriteString("\n")
	}

	if watchdog != nil {
		services := watchdog.GetServices()
		if len(services) > 0 {
			sb.WriteString("## 4. Microservice Health & Crash Diagnosis\n\n")
			sb.WriteString("| Service | Probe URL | Status | Last Response Time | Failure Point |\n")
			sb.WriteString("|---|---|---|---|---|\n")
			for _, s := range services {
				failedAt := "N/A"
				if s.FailedAtVU > 0 {
					failedAt = fmt.Sprintf("At %d VUs", s.FailedAtVU)
				}
				sb.WriteString(fmt.Sprintf("| **%s** | `%s` | `%s` | %v | %s |\n",
					s.Name, s.URL, s.Status, s.LastLatency.Round(time.Millisecond), failedAt))
			}
			sb.WriteString("\n")
		}

		failures := watchdog.GetFailureLog()
		if len(failures) > 0 {
			sb.WriteString("### Service Failure Event Log\n\n")
			for _, f := range failures {
				sb.WriteString(fmt.Sprintf("- ⚠️ %s\n", f))
			}
			sb.WriteString("\n")
		}
	}

	if len(report.EndpointStats) > 0 {
		sb.WriteString("## 5. Per-Endpoint Performance Breakdown\n\n")
		sb.WriteString("| Method | Endpoint | Total Requests | Error Count | Avg Latency | P95 Latency | P99 Latency |\n")
		sb.WriteString("|---|---|---|---|---|---|---|\n")
		for _, ep := range report.EndpointStats {
			sb.WriteString(fmt.Sprintf("| `%s` | `%s` | %d | %d | %v | %v | %v |\n",
				ep.Method, ep.Endpoint, ep.TotalReqs, ep.ErrorCount, ep.AvgDuration.Round(time.Millisecond), ep.P95Duration.Round(time.Millisecond), ep.P99Duration.Round(time.Millisecond)))
		}
		sb.WriteString("\n")
	}

	return os.WriteFile(filePath, []byte(sb.String()), 0644)
}

