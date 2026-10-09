package auth

import (
	"context"
	"encoding/json"
	"fmt"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
	"go-gateway/internal/config"
)

func getTestRedisClient(t *testing.T) *redis.Client {
	t.Helper()
	cfg := config.Load("8083")
	addr := fmt.Sprintf("%s:%s", cfg.RedisHost, cfg.RedisPort)

	rdb := redis.NewClient(&redis.Options{
		Addr:     addr,
		Password: cfg.RedisPassword,
		DB:       0,
	})

	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()

	if err := rdb.Ping(ctx).Err(); err != nil {
		t.Skipf("Redis not reachable at %s, skipping live Redis test: %v", addr, err)
		return nil
	}

	return rdb
}

func TestValidateAndConsumeTicket_LiveRedis(t *testing.T) {
	rdb := getTestRedisClient(t)
	defer rdb.Close()

	ctx := context.Background()
	ticketID := "test-ticket-" + uuid.New().String()
	userID := uuid.New()
	roles := []string{"Courier", "Customer"}

	// 1. Simulate ticket written by ASP.NET WebSocketTicketService.cs
	dotnetTicketJSON := map[string]interface{}{
		"userId":    userID,
		"roles":     roles,
		"issuedAt":  time.Now().UTC(),
		"expiresAt": time.Now().UTC().Add(60 * time.Second),
	}
	bytes, _ := json.Marshal(dotnetTicketJSON)
	key := fmt.Sprintf("ws:ticket:%s", ticketID)

	err := rdb.Set(ctx, key, string(bytes), 60*time.Second).Err()
	if err != nil {
		t.Fatalf("failed to seed Redis ticket: %v", err)
	}

	// 2. Consume ticket via Go auth validator
	payload, err := ValidateAndConsumeTicket(ctx, rdb, ticketID)
	if err != nil {
		t.Fatalf("ValidateAndConsumeTicket failed: %v", err)
	}

	if payload.UserID != userID {
		t.Errorf("expected UserID %v, got %v", userID, payload.UserID)
	}
	if len(payload.Roles) != 2 || payload.Roles[0] != "Courier" {
		t.Errorf("expected Roles [Courier, Customer], got %v", payload.Roles)
	}

	// 3. Verify single-use enforcement: second consumption MUST fail
	_, err = ValidateAndConsumeTicket(ctx, rdb, ticketID)
	if err != ErrTicketNotFound {
		t.Fatalf("expected ErrTicketNotFound on second use, got: %v", err)
	}
}

func TestValidateAndConsumeTicket_ExpiredRejection(t *testing.T) {
	rdb := getTestRedisClient(t)
	defer rdb.Close()

	ctx := context.Background()
	ticketID := "expired-ticket-" + uuid.New().String()
	userID := uuid.New()

	// Seed ticket with expiration in the past
	dotnetTicketJSON := map[string]interface{}{
		"userId":    userID,
		"roles":     []string{"Customer"},
		"issuedAt":  time.Now().UTC().Add(-2 * time.Minute),
		"expiresAt": time.Now().UTC().Add(-1 * time.Minute),
	}
	bytes, _ := json.Marshal(dotnetTicketJSON)
	key := fmt.Sprintf("ws:ticket:%s", ticketID)

	_ = rdb.Set(ctx, key, string(bytes), 60*time.Second).Err()

	_, err := ValidateAndConsumeTicket(ctx, rdb, ticketID)
	if err != ErrTicketExpired {
		t.Fatalf("expected ErrTicketExpired, got: %v", err)
	}
}
