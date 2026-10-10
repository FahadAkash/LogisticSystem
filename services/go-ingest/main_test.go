package main

import (
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestIngestHealthEndpoints(t *testing.T) {
	logger := slog.New(slog.NewJSONHandler(io.Discard, nil))
	router := newRouter(logger, nil)

	tests := []struct {
		name           string
		path           string
		expectedStatus int
		expectedField  string
	}{
		{"health endpoint", "/health", http.StatusOK, "healthy"},
		{"ready endpoint", "/ready", http.StatusOK, "ready"},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			req := httptest.NewRequest("GET", tc.path, nil)
			rr := httptest.NewRecorder()

			router.ServeHTTP(rr, req)

			if rr.Code != tc.expectedStatus {
				t.Fatalf("expected status %d, got %d", tc.expectedStatus, rr.Code)
			}

			var resp HealthResponse
			if err := json.NewDecoder(rr.Body).Decode(&resp); err != nil {
				t.Fatalf("failed to decode response: %v", err)
			}

			if resp.Status != tc.expectedField {
				t.Fatalf("expected status %q, got %q", tc.expectedField, resp.Status)
			}
			if resp.Service != "go-ingest" {
				t.Fatalf("expected service go-ingest, got %q", resp.Service)
			}
		})
	}
}

