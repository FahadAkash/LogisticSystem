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

	"github.com/gorilla/websocket"
	"github.com/prometheus/client_golang/prometheus/promhttp"
	"github.com/redis/go-redis/v9"
	"go-gateway/internal/auth"
	"go-gateway/internal/config"
)

type HealthResponse struct {
	Status    string `json:"status"`
	Service   string `json:"service"`
	Timestamp string `json:"timestamp"`
}

var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool {
		return true // Allow all origins for dev/load testing
	},
	ReadBufferSize:  1024,
	WriteBufferSize: 1024,
}

func newRouter(logger *slog.Logger, rdb *redis.Client) http.Handler {
	mux := http.NewServeMux()

	// Prometheus metrics per Agent.md Rule 12.10 & 15.1
	mux.Handle("GET /metrics", promhttp.Handler())
	mux.Handle("GET /ws/metrics", promhttp.Handler())

	healthHandler := func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		_ = json.NewEncoder(w).Encode(HealthResponse{
			Status:    "healthy",
			Service:   "go-gateway",
			Timestamp: time.Now().UTC().Format(time.RFC3339),
		})
	}
	mux.HandleFunc("GET /health", healthHandler)
	mux.HandleFunc("GET /ws/health", healthHandler)

	readyHandler := func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		_ = json.NewEncoder(w).Encode(HealthResponse{
			Status:    "ready",
			Service:   "go-gateway",
			Timestamp: time.Now().UTC().Format(time.RFC3339),
		})
	}
	mux.HandleFunc("GET /ready", readyHandler)
	mux.HandleFunc("GET /ws/ready", readyHandler)

	mux.HandleFunc("GET /ws", func(w http.ResponseWriter, r *http.Request) {
		ticket := r.URL.Query().Get("ticket")
		if ticket == "" {
			http.Error(w, `{"error":"ticket query parameter required"}`, http.StatusUnauthorized)
			return
		}

		if rdb == nil {
			http.Error(w, `{"error":"redis connection unavailable"}`, http.StatusServiceUnavailable)
			return
		}

		// Consume and validate ticket atomically
		payload, err := auth.ValidateAndConsumeTicket(r.Context(), rdb, ticket)
		if err != nil {
			logger.Warn("invalid or expired websocket ticket", slog.String("error", err.Error()), slog.String("ticket", ticket))
			http.Error(w, fmt.Sprintf(`{"error":"%s"}`, err.Error()), http.StatusUnauthorized)
			return
		}

		conn, err := upgrader.Upgrade(w, r, nil)
		if err != nil {
			logger.Error("failed to upgrade websocket connection", slog.String("error", err.Error()))
			return
		}
		defer conn.Close()

		logger.Info("websocket client connected", slog.String("userId", payload.UserID.String()))

		// Send initial welcome frame
		welcome := map[string]interface{}{
			"event":     "connected",
			"userId":    payload.UserID.String(),
			"roles":     payload.Roles,
			"timestamp": time.Now().UTC(),
		}
		if err := conn.WriteJSON(welcome); err != nil {
			return
		}

		// Keep connection open and read messages/pings
		for {
			msgType, msg, err := conn.ReadMessage()
			if err != nil {
				break
			}
			if msgType == websocket.PingMessage {
				_ = conn.WriteMessage(websocket.PongMessage, []byte("pong"))
			} else if msgType == websocket.TextMessage {
				// Echo or acknowledge
				_ = conn.WriteJSON(map[string]interface{}{
					"event": "ack",
					"size":  len(msg),
				})
			}
		}
	})

	mux.HandleFunc("GET /", func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/" {
			http.NotFound(w, r)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]string{
			"service": "go-gateway",
			"status":  "running",
			"desc":    "Realtime WebSocket & Fan-out Gateway",
		})
	})

	return mux
}

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{
		Level: slog.LevelInfo,
	})).With("service", "go-gateway")

	cfg := config.Load("8083")
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
		ReadTimeout:       60 * time.Second,
		WriteTimeout:      60 * time.Second,
		IdleTimeout:       120 * time.Second,
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
