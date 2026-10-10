package metrics

import (
	"time"
)

// RequestResult holds the outcome of a single request.
type RequestResult struct {
	Endpoint   string
	Method     string
	StatusCode int
	Duration   time.Duration
	Err        error
	BytesRead  int64
	Timestamp  time.Time
}

// EndpointStats aggregates statistics for a specific API endpoint.
type EndpointStats struct {
	Endpoint     string        `json:"endpoint"`
	Method       string        `json:"method"`
	TotalReqs    int64         `json:"totalRequests"`
	SuccessCount int64         `json:"successCount"`
	ErrorCount   int64         `json:"errorCount"`
	MinDuration  time.Duration `json:"minDuration"`
	MaxDuration  time.Duration `json:"maxDuration"`
	AvgDuration  time.Duration `json:"avgDuration"`
	P50Duration  time.Duration `json:"p50Duration"`
	P90Duration  time.Duration `json:"p90Duration"`
	P95Duration  time.Duration `json:"p95Duration"`
	P99Duration  time.Duration `json:"p99Duration"`
	RPS          float64       `json:"rps"`
}

// StepMetric records performance metrics for a specific concurrent user tier.
type StepMetric struct {
	VUs           int           `json:"vus"`
	RPS           float64       `json:"rps"`
	TotalRequests int64         `json:"totalRequests"`
	ErrorCount    int64         `json:"errorCount"`
	ErrorRate     float64       `json:"errorRate"`
	P50           time.Duration `json:"p50"`
	P90           time.Duration `json:"p90"`
	P95           time.Duration `json:"p95"`
	P99           time.Duration `json:"p99"`
	Timestamp     time.Time     `json:"timestamp"`
	HealthStatus  string        `json:"healthStatus"`
}

// SummaryReport represents the final aggregated benchmark report.
type SummaryReport struct {
	TargetURL       string                    `json:"targetUrl"`
	Mode            string                    `json:"mode"`
	StartTime       time.Time                 `json:"startTime"`
	EndTime         time.Time                 `json:"endTime"`
	TotalDuration   time.Duration             `json:"totalDuration"`
	TotalRequests   int64                     `json:"totalRequests"`
	SuccessfulReqs  int64                     `json:"successfulRequests"`
	FailedReqs      int64                     `json:"failedRequests"`
	ErrorRate       float64                   `json:"errorRate"`
	OverallRPS      float64                   `json:"overallRps"`
	PeakRPS         float64                   `json:"peakRps"`
	PeakVUs         int                       `json:"peakVus"`
	BreakingPointVU int                       `json:"breakingPointVu"` // VU where error rate crossed 5%
	MinLatency      time.Duration             `json:"minLatency"`
	AvgLatency      time.Duration             `json:"avgLatency"`
	MaxLatency      time.Duration             `json:"maxLatency"`
	P50Latency      time.Duration             `json:"p50Latency"`
	P90Latency      time.Duration             `json:"p90Latency"`
	P95Latency      time.Duration             `json:"p95Latency"`
	P99Latency      time.Duration             `json:"p99Latency"`
	StatusCodes     map[int]int64             `json:"statusCodes"`
	ErrorBreakdown  map[string]int64          `json:"errorBreakdown"`
	EndpointStats   map[string]*EndpointStats `json:"endpointStats"`
	Steps           []StepMetric              `json:"steps"`
	Diagnosis       string                    `json:"diagnosis"`
}
