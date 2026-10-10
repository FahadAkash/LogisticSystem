package monitor

import (
	"context"
	"fmt"
	"net/http"
	"strings"
	"sync"
	"time"
)


// ServiceHealth tracks health state of an individual microservice.
type ServiceHealth struct {
	Name         string        `json:"name"`
	URL          string        `json:"url"`
	Status       string        `json:"status"` // HEALTHY, DEGRADED, DOWN
	LastLatency  time.Duration `json:"lastLatency"`
	FailureCount int           `json:"failureCount"`
	FailedAtVU   int           `json:"failedAtVu,omitempty"`
	LastError    string        `json:"lastError,omitempty"`
}

// Watchdog periodically probes microservices in the background during load tests.
type Watchdog struct {
	mu           sync.RWMutex
	services     []*ServiceHealth
	httpClient   *http.Client
	stopChan     chan struct{}
	failureLog   []string
	activeVUFunc func() int
}

// NewWatchdog creates an initialized health monitoring probe.
func NewWatchdog(targetURL, ingestURL, gatewayURL, dispatchURL, etaURL string, activeVUFunc func() int) *Watchdog {
	client := &http.Client{
		Timeout: 3 * time.Second,
	}

	// gateway HTTP health URL is derived from gatewayURL (replace ws:// with http://)
	gatewayHttp := strings.Replace(gatewayURL, "ws://", "http://", 1)
	gatewayHttp = strings.Replace(gatewayHttp, "wss://", "https://", 1)

	services := []*ServiceHealth{
		{Name: "ASP.NET Core API", URL: targetURL + "/health", Status: "UNKNOWN"},
		{Name: "Identity / JWKS", URL: targetURL + "/api/auth/jwks", Status: "UNKNOWN"},
		{Name: "Go Location Ingest", URL: ingestURL + "/health", Status: "UNKNOWN"},
		{Name: "Go Dispatch Engine", URL: dispatchURL + "/health", Status: "UNKNOWN"},
		{Name: "Go Realtime Gateway", URL: gatewayHttp + "/health", Status: "UNKNOWN"},
		{Name: "Go ETA Engine", URL: etaURL + "/health", Status: "UNKNOWN"},
	}


	return &Watchdog{
		services:     services,
		httpClient:   client,
		stopChan:     make(chan struct{}),
		failureLog:   make([]string, 0),
		activeVUFunc: activeVUFunc,
	}
}

// Start initiates the background probe loop running at 1-second intervals.
func (w *Watchdog) Start() {
	go func() {
		ticker := time.NewTicker(1 * time.Second)
		defer ticker.Stop()

		for {
			select {
			case <-w.stopChan:
				return
			case <-ticker.C:
				w.probeAll()
			}
		}
	}()
}

// Stop shuts down the background probe loop.
func (w *Watchdog) Stop() {
	close(w.stopChan)
}

func (w *Watchdog) probeAll() {
	w.mu.Lock()
	defer w.mu.Unlock()

	currentVUs := 0
	if w.activeVUFunc != nil {
		currentVUs = w.activeVUFunc()
	}

	for _, svc := range w.services {
		start := time.Now()
		req, err := http.NewRequestWithContext(context.Background(), "GET", svc.URL, nil)
		if err != nil {
			continue
		}

		resp, err := w.httpClient.Do(req)
		dur := time.Since(start)
		svc.LastLatency = dur

		if err != nil || (resp != nil && resp.StatusCode >= 500) {
			svc.FailureCount++
			errText := "connection error / timeout"
			if resp != nil {
				errText = fmt.Sprintf("HTTP %d", resp.StatusCode)
			} else if err != nil {
				errText = err.Error()
			}

			if svc.Status != "DOWN" {
				svc.Status = "DOWN"
				svc.FailedAtVU = currentVUs
				svc.LastError = errText
				event := fmt.Sprintf("[%s] %s went DOWN at %d VUs (Reason: %s)", time.Now().Format("15:04:05"), svc.Name, currentVUs, errText)
				w.failureLog = append(w.failureLog, event)
			}
		} else if dur > 1500*time.Millisecond {
			svc.Status = "DEGRADED"
			if svc.FailedAtVU == 0 {
				svc.FailedAtVU = currentVUs
			}
		} else {
			svc.Status = "HEALTHY"
		}

		if resp != nil {
			_ = resp.Body.Close()
		}
	}
}

// GetStatusSnapshot returns the latest status string for terminal displays.
func (w *Watchdog) GetStatusSnapshot() string {
	w.mu.RLock()
	defer w.mu.RUnlock()

	downCount := 0
	degradedCount := 0
	for _, svc := range w.services {
		if svc.Status == "DOWN" {
			downCount++
		} else if svc.Status == "DEGRADED" {
			degradedCount++
		}
	}

	if downCount > 0 {
		return fmt.Sprintf("DEGRADED (%d down, %d stressed)", downCount, degradedCount)
	}
	if degradedCount > 0 {
		return fmt.Sprintf("STRESSED (%d degraded)", degradedCount)
	}
	return "ALL SERVICES HEALTHY"
}

// GetServices returns current health metrics for all services.
func (w *Watchdog) GetServices() []ServiceHealth {
	w.mu.RLock()
	defer w.mu.RUnlock()

	result := make([]ServiceHealth, len(w.services))
	for i, s := range w.services {
		result[i] = *s
	}
	return result
}

// GetFailureLog returns the chronological record of service failures.
func (w *Watchdog) GetFailureLog() []string {
	w.mu.RLock()
	defer w.mu.RUnlock()

	result := make([]string, len(w.failureLog))
	copy(result, w.failureLog)
	return result
}
