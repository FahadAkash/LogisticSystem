package metrics

import (
	"fmt"
	"sort"
	"strings"
	"sync"
	"sync/atomic"
	"time"
)

// Collector safely aggregates real-time metrics across concurrent worker goroutines.
type Collector struct {
	mu             sync.RWMutex
	targetURL      string
	mode           string
	startTime      time.Time
	activeVUs      int32
	totalRequests  int64
	successfulReqs int64
	failedReqs     int64

	statusCodes    map[int]int64
	errorBreakdown map[string]int64
	latencies      []time.Duration
	endpointMap    map[string]*endpointCollector
	steps          []StepMetric

	lastSampleTime time.Time
	lastReqCount   int64
	peakRPS        float64
}

type endpointCollector struct {
	mu           sync.Mutex
	method       string
	totalReqs    int64
	successCount int64
	errorCount   int64
	latencies    []time.Duration
}

// NewCollector creates an initialized metrics collector.
func NewCollector(targetURL, mode string) *Collector {
	now := time.Now()
	return &Collector{
		targetURL:      targetURL,
		mode:           mode,
		startTime:      now,
		lastSampleTime: now,
		statusCodes:    make(map[int]int64),
		errorBreakdown: make(map[string]int64),
		latencies:      make([]time.Duration, 0, 100000),
		endpointMap:    make(map[string]*endpointCollector),
		steps:          make([]StepMetric, 0),
	}
}

// SetActiveVUs updates the currently running concurrent worker count.
func (c *Collector) SetActiveVUs(vus int) {
	atomic.StoreInt32(&c.activeVUs, int32(vus))
}

// GetActiveVUs returns current concurrent worker count.
func (c *Collector) GetActiveVUs() int {
	return int(atomic.LoadInt32(&c.activeVUs))
}

// Record saves the result of an HTTP or WebSocket operation.
func (c *Collector) Record(res RequestResult) {
	atomic.AddInt64(&c.totalRequests, 1)

	isError := res.Err != nil || res.StatusCode >= 400 || res.StatusCode == 0

	if isError {
		atomic.AddInt64(&c.failedReqs, 1)
	} else {
		atomic.AddInt64(&c.successfulReqs, 1)
	}

	c.mu.Lock()
	if res.StatusCode > 0 {
		c.statusCodes[res.StatusCode]++
	} else {
		c.statusCodes[0]++ // 0 indicates network drop / connection error
	}

	if res.Err != nil {
		errStr := res.Err.Error()
		if strings.Contains(errStr, "timeout") || strings.Contains(errStr, "deadline") {
			c.errorBreakdown["Timeout"]++
		} else if strings.Contains(errStr, "connection refused") {
			c.errorBreakdown["ConnectionRefused"]++
		} else if strings.Contains(errStr, "connection reset") {
			c.errorBreakdown["ConnectionReset"]++
		} else {
			c.errorBreakdown["NetworkError"]++
		}
	} else if res.StatusCode >= 500 {
		if res.StatusCode == 524 {
			c.errorBreakdown["CloudflareTimeout_524"]++
		} else if res.StatusCode == 521 {
			c.errorBreakdown["CloudflareOriginDown_521"]++
		} else if res.StatusCode == 500 {
			c.errorBreakdown["InternalServerError_500"]++
		} else if res.StatusCode == 502 {
			c.errorBreakdown["BadGateway_502"]++
		} else if res.StatusCode == 503 {
			c.errorBreakdown["ServiceUnavailable_503"]++
		} else {
			c.errorBreakdown[fmt.Sprintf("HTTP_%d", res.StatusCode)]++
		}
	} else if res.StatusCode == 429 {
		c.errorBreakdown["RateLimited_429"]++
	}

	c.latencies = append(c.latencies, res.Duration)

	epKey := fmt.Sprintf("%s %s", res.Method, res.Endpoint)
	epCol, exists := c.endpointMap[epKey]
	if !exists {
		epCol = &endpointCollector{
			method:    res.Method,
			latencies: make([]time.Duration, 0, 10000),
		}
		c.endpointMap[epKey] = epCol
	}
	c.mu.Unlock()

	epCol.mu.Lock()
	epCol.totalReqs++
	if isError {
		epCol.errorCount++
	} else {
		epCol.successCount++
	}
	epCol.latencies = append(epCol.latencies, res.Duration)
	epCol.mu.Unlock()
}

// RecordStep records a snapshot for a specific VU stage in stepped stress tests.
func (c *Collector) RecordStep(vus int, healthStatus string) {
	c.mu.Lock()
	defer c.mu.Unlock()

	now := time.Now()
	elapsed := now.Sub(c.lastSampleTime).Seconds()
	if elapsed <= 0 {
		elapsed = 1
	}

	reqsInStep := atomic.LoadInt64(&c.totalRequests) - c.lastReqCount
	stepRPS := float64(reqsInStep) / elapsed
	if stepRPS > c.peakRPS {
		c.peakRPS = stepRPS
	}

	c.lastSampleTime = now
	c.lastReqCount = atomic.LoadInt64(&c.totalRequests)

	// Calculate recent latencies if available
	p50, p90, p95, p99 := calcPercentiles(c.latencies)

	errRate := 0.0
	total := atomic.LoadInt64(&c.totalRequests)
	failed := atomic.LoadInt64(&c.failedReqs)
	if total > 0 {
		errRate = (float64(failed) / float64(total)) * 100.0
	}

	step := StepMetric{
		VUs:           vus,
		RPS:           stepRPS,
		TotalRequests: total,
		ErrorCount:    failed,
		ErrorRate:     errRate,
		P50:           p50,
		P90:           p90,
		P95:           p95,
		P99:           p99,
		Timestamp:     now,
		HealthStatus:  healthStatus,
	}

	c.steps = append(c.steps, step)
}

// CurrentStats calculates quick snapshot metrics for the real-time console dashboard.
func (c *Collector) CurrentStats() (total int64, success int64, failed int64, rps float64, p95 time.Duration) {
	total = atomic.LoadInt64(&c.totalRequests)
	success = atomic.LoadInt64(&c.successfulReqs)
	failed = atomic.LoadInt64(&c.failedReqs)

	now := time.Now()
	totalElapsed := now.Sub(c.startTime).Seconds()
	if totalElapsed > 0 {
		rps = float64(total) / totalElapsed
	}

	c.mu.RLock()
	if len(c.latencies) > 0 {
		idx := int(float64(len(c.latencies)) * 0.95)
		if idx >= len(c.latencies) {
			idx = len(c.latencies) - 1
		}
		p95 = c.latencies[idx]
	}
	c.mu.RUnlock()

	return total, success, failed, rps, p95
}

// GenerateSummary builds the complete post-test analysis report.
func (c *Collector) GenerateSummary() SummaryReport {
	c.mu.Lock()
	defer c.mu.Unlock()

	endTime := time.Now()
	totalDuration := endTime.Sub(c.startTime)

	total := atomic.LoadInt64(&c.totalRequests)
	success := atomic.LoadInt64(&c.successfulReqs)
	failed := atomic.LoadInt64(&c.failedReqs)

	errRate := 0.0
	overallRPS := 0.0
	if total > 0 {
		errRate = (float64(failed) / float64(total)) * 100.0
	}
	if totalDuration.Seconds() > 0 {
		overallRPS = float64(total) / totalDuration.Seconds()
	}

	sort.Slice(c.latencies, func(i, j int) bool {
		return c.latencies[i] < c.latencies[j]
	})

	var minLat, maxLat, avgLat time.Duration
	p50, p90, p95, p99 := calcPercentilesSorted(c.latencies)

	if len(c.latencies) > 0 {
		minLat = c.latencies[0]
		maxLat = c.latencies[len(c.latencies)-1]
		var sum time.Duration
		for _, lat := range c.latencies {
			sum += lat
		}
		avgLat = sum / time.Duration(len(c.latencies))
	}

	breakingPointVU := 0
	peakVUs := 0
	for _, step := range c.steps {
		if step.VUs > peakVUs {
			peakVUs = step.VUs
		}
		if step.ErrorRate >= 5.0 && breakingPointVU == 0 {
			breakingPointVU = step.VUs
		}
	}

	// Compute endpoint-level stats
	endpointStats := make(map[string]*EndpointStats)
	for key, col := range c.endpointMap {
		col.mu.Lock()
		sort.Slice(col.latencies, func(i, j int) bool {
			return col.latencies[i] < col.latencies[j]
		})
		epP50, epP90, epP95, epP99 := calcPercentilesSorted(col.latencies)
		var epMin, epMax, epAvg time.Duration
		if len(col.latencies) > 0 {
			epMin = col.latencies[0]
			epMax = col.latencies[len(col.latencies)-1]
			var epSum time.Duration
			for _, l := range col.latencies {
				epSum += l
			}
			epAvg = epSum / time.Duration(len(col.latencies))
		}
		epRPS := 0.0
		if totalDuration.Seconds() > 0 {
			epRPS = float64(col.totalReqs) / totalDuration.Seconds()
		}

		parts := strings.SplitN(key, " ", 2)
		endpointStats[key] = &EndpointStats{
			Method:       parts[0],
			Endpoint:     parts[1],
			TotalReqs:    col.totalReqs,
			SuccessCount: col.successCount,
			ErrorCount:   col.errorCount,
			MinDuration:  epMin,
			MaxDuration:  epMax,
			AvgDuration:  epAvg,
			P50Duration:  epP50,
			P90Duration:  epP90,
			P95Duration:  epP95,
			P99Duration:  epP99,
			RPS:          epRPS,
		}
		col.mu.Unlock()
	}

	diagnosis := c.deriveDiagnosis(errRate, breakingPointVU)

	return SummaryReport{
		TargetURL:       c.targetURL,
		Mode:            c.mode,
		StartTime:       c.startTime,
		EndTime:         endTime,
		TotalDuration:   totalDuration,
		TotalRequests:   total,
		SuccessfulReqs:  success,
		FailedReqs:      failed,
		ErrorRate:       errRate,
		OverallRPS:      overallRPS,
		PeakRPS:         c.peakRPS,
		PeakVUs:         peakVUs,
		BreakingPointVU: breakingPointVU,
		MinLatency:      minLat,
		AvgLatency:      avgLat,
		MaxLatency:      maxLat,
		P50Latency:      p50,
		P90Latency:      p90,
		P95Latency:      p95,
		P99Latency:      p99,
		StatusCodes:     c.statusCodes,
		ErrorBreakdown:  c.errorBreakdown,
		EndpointStats:   endpointStats,
		Steps:           c.steps,
		Diagnosis:       diagnosis,
	}
}

func (c *Collector) deriveDiagnosis(errRate float64, breakingVU int) string {
	cf524 := c.errorBreakdown["CloudflareTimeout_524"]
	cf521 := c.errorBreakdown["CloudflareOriginDown_521"]
	err500 := c.errorBreakdown["InternalServerError_500"]
	rate429 := c.errorBreakdown["RateLimited_429"]
	timeouts := c.errorBreakdown["Timeout"]
	connRefused := c.errorBreakdown["ConnectionRefused"]

	if errRate < 1.0 {
		return "EXCELLENT: Platform sustained load with zero degradation. Error rate under 1%."
	}

	if cf524 > 0 || cf521 > 0 {
		return fmt.Sprintf("CLOUDFLARE TUNNEL BOTTLENECK: Encountered %d Cloudflare 524 timeouts / 521 origin drops. The origin server was overwhelmed or Cloudflare rate-limited the connection tunnel at ~%d concurrent users.", cf524+cf521, breakingVU)
	}

	if err500 > 0 {
		return fmt.Sprintf("BACKEND EXHAUSTION (HTTP 500): Encountered %d server errors. Likely PostgreSQL connection pool saturation, transactional outbox contention, or unhandled exceptions under concurrency at ~%d concurrent users.", err500, breakingVU)
	}

	if rate429 > 0 {
		return fmt.Sprintf("RATE LIMITING TRIGGERED (HTTP 429): %d requests were throttled by rate limiters at ~%d concurrent users.", rate429, breakingVU)
	}

	if timeouts > 0 || connRefused > 0 {
		return fmt.Sprintf("TCP / SOCKET EXHAUSTION: %d timeouts and %d connection refusals. Server TCP accept backlog was full or ASP.NET thread pool was starved at ~%d concurrent users.", timeouts, connRefused, breakingVU)
	}

	return fmt.Sprintf("DEGRADED: System performance degraded above tolerable thresholds at ~%d concurrent users (Error Rate: %.2f%%).", breakingVU, errRate)
}

func calcPercentiles(latencies []time.Duration) (p50, p90, p95, p99 time.Duration) {
	if len(latencies) == 0 {
		return 0, 0, 0, 0
	}
	cp := make([]time.Duration, len(latencies))
	copy(cp, latencies)
	sort.Slice(cp, func(i, j int) bool { return cp[i] < cp[j] })
	return calcPercentilesSorted(cp)
}

func calcPercentilesSorted(sorted []time.Duration) (p50, p90, p95, p99 time.Duration) {
	n := len(sorted)
	if n == 0 {
		return 0, 0, 0, 0
	}

	idx50 := int(float64(n) * 0.50)
	idx90 := int(float64(n) * 0.90)
	idx95 := int(float64(n) * 0.95)
	idx99 := int(float64(n) * 0.99)

	if idx50 >= n { idx50 = n - 1 }
	if idx90 >= n { idx90 = n - 1 }
	if idx95 >= n { idx95 = n - 1 }
	if idx99 >= n { idx99 = n - 1 }

	return sorted[idx50], sorted[idx90], sorted[idx95], sorted[idx99]
}
