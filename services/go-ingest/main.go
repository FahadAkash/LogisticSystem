package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/google/uuid"
	"github.com/prometheus/client_golang/prometheus/promhttp"
	"github.com/redis/go-redis/v9"
	"go-ingest/internal/config"
)

type HealthResponse struct {
	Status    string `json:"status"`
	Service   string `json:"service"`
	Timestamp string `json:"timestamp"`
}

type IngestPingRequest struct {
	CourierID    *uuid.UUID `json:"courierId,omitempty"`
	CourierIDAlt *uuid.UUID `json:"courier_id,omitempty"`
	Latitude     float64    `json:"latitude"`
	Longitude    float64    `json:"longitude"`
	Speed        *float64   `json:"speed,omitempty"`
	SpeedKmh     *float32   `json:"speed_kmh,omitempty"`
	Heading      *float32   `json:"heading,omitempty"`
	Timestamp    *time.Time `json:"timestamp,omitempty"`
}

func newRouter(logger *slog.Logger, rdb *redis.Client) http.Handler {
	mux := http.NewServeMux()

	// Prometheus metrics per Agent.md Rule 12.10 & 15.1
	mux.Handle("GET /metrics", promhttp.Handler())
	mux.Handle("GET /ingest/metrics", promhttp.Handler())

	healthHandler := func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		_ = json.NewEncoder(w).Encode(HealthResponse{
			Status:    "healthy",
			Service:   "go-ingest",
			Timestamp: time.Now().UTC().Format(time.RFC3339),
		})
	}
	mux.HandleFunc("GET /health", healthHandler)
	mux.HandleFunc("GET /ingest/health", healthHandler)

	readyHandler := func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		_ = json.NewEncoder(w).Encode(HealthResponse{
			Status:    "ready",
			Service:   "go-ingest",
			Timestamp: time.Now().UTC().Format(time.RFC3339),
		})
	}
	mux.HandleFunc("GET /ready", readyHandler)
	mux.HandleFunc("GET /ingest/ready", readyHandler)

	locationHandler := func(w http.ResponseWriter, r *http.Request) {
		var ping IngestPingRequest
		if err := json.NewDecoder(r.Body).Decode(&ping); err != nil {
			http.Error(w, `{"error":"invalid json"}`, http.StatusBadRequest)
			return
		}

		cID := uuid.Nil
		if ping.CourierID != nil && *ping.CourierID != uuid.Nil {
			cID = *ping.CourierID
		} else if ping.CourierIDAlt != nil && *ping.CourierIDAlt != uuid.Nil {
			cID = *ping.CourierIDAlt
		}

		if cID == uuid.Nil {
			http.Error(w, `{"error":"courierId is required"}`, http.StatusBadRequest)
			return
		}

		if ping.Latitude < -90 || ping.Latitude > 90 || ping.Longitude < -180 || ping.Longitude > 180 {
			http.Error(w, `{"error":"invalid coordinates"}`, http.StatusBadRequest)
			return
		}

		if rdb != nil {
			// Update spatial index: GEOADD couriers:geo <lon> <lat> <courierId>
			_ = rdb.GeoAdd(r.Context(), "couriers:geo", &redis.GeoLocation{
				Name:      cID.String(),
				Longitude: ping.Longitude,
				Latitude:  ping.Latitude,
			}).Err()
		}

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusAccepted)
		_ = json.NewEncoder(w).Encode(map[string]string{
			"status":    "accepted",
			"courierId": cID.String(),
		})
	}

	mux.HandleFunc("POST /api/v1/location", locationHandler)
	mux.HandleFunc("POST /location", locationHandler)
	mux.HandleFunc("POST /ingest/api/v1/location", locationHandler)
	mux.HandleFunc("POST /ingest/location", locationHandler)

	mux.HandleFunc("GET /", func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/" && r.URL.Path != "/ingest" && r.URL.Path != "/ingest/" {
			http.NotFound(w, r)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]string{
			"service": "go-ingest",
			"status":  "running",
			"desc":    "Location Ingestion Service",
		})
	})

	return mux
}

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{
		Level: slog.LevelInfo,
	})).With("service", "go-ingest")

	cfg := config.Load("8081")
	logger.Info("configuration loaded", slog.Any("config", cfg.SafeSummary()))

	var rdb *redis.Client
	if cfg.RedisHost != "" {
		redisAddr := fmt.Sprintf("%s:%s", cfg.RedisHost, cfg.RedisPort)
		rdb = redis.NewClient(&redis.Options{
			Addr:     redisAddr,
			Password: cfg.RedisPassword,
			DB:       0,
		})
		logger.Info("connected to redis", slog.String("addr", redisAddr))
	}

	addr := fmt.Sprintf(":%s", cfg.Port)

	server := &http.Server{
		Addr:              addr,
		Handler:           newRouter(logger, rdb),
		ReadHeaderTimeout: 5 * time.Second,
		ReadTimeout:       10 * time.Second,
		WriteTimeout:      10 * time.Second,
		IdleTimeout:       30 * time.Second,
	}

	serverErrors := make(chan error, 1)
	go func() {
		logger.Info("starting server", slog.String("addr", addr))
		if err := server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			serverErrors <- fmt.Errorf("http server failure: %w", err)
		}
	}()

	shutdown := make(chan os.Signal, 1)
	signal.Notify(shutdown, os.Interrupt, syscall.SIGTERM)

	select {
	case err := <-serverErrors:
		logger.Error("server failed to start", slog.String("error", err.Error()))
		os.Exit(1)
	case sig := <-shutdown:
		logger.Info("shutdown signal received", slog.String("signal", sig.String()))

		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		if err := server.Shutdown(ctx); err != nil {
			logger.Error("graceful shutdown failed, forcing close", slog.String("error", err.Error()))
			_ = server.Close()
		}
		if rdb != nil {
			_ = rdb.Close()
		}
		logger.Info("server stopped gracefully")
	}
}
