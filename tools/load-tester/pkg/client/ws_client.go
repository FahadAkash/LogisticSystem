package client

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"time"

	"github.com/gorilla/websocket"
	"load-tester/pkg/metrics"
)

// WSTicketResponse matches POST /api/auth/ws-ticket JSON payload.
type WSTicketResponse struct {
	Ticket    string    `json:"ticket"`
	ExpiresIn int       `json:"expiresIn"`
	IssuedAt  time.Time `json:"issuedAt"`
}

// WebSocketClient manages single-use ticket acquisition and WebSocket lifecycle.
type WebSocketClient struct {
	HttpClient *TargetClient
	GatewayURL string
}

// NewWebSocketClient creates a WebSocket testing client.
func NewWebSocketClient(httpClient *TargetClient, gatewayURL string) *WebSocketClient {
	return &WebSocketClient{
		HttpClient: httpClient,
		GatewayURL: gatewayURL,
	}
}

// AcquireTicket requests a single-use 60s ticket from the API.
func (ws *WebSocketClient) AcquireTicket(ctx context.Context, token string) (string, metrics.RequestResult, error) {
	res, data := ws.HttpClient.Do(ctx, "POST", "/api/auth/ws-ticket", nil, nil, token)
	if res.StatusCode != http.StatusOK {
		return "", res, fmt.Errorf("failed to acquire ws ticket: status %d", res.StatusCode)
	}

	var ticketResp WSTicketResponse
	if err := json.Unmarshal(data, &ticketResp); err != nil {
		return "", res, fmt.Errorf("failed to parse ws ticket response: %w", err)
	}

	return ticketResp.Ticket, res, nil
}

// ConnectAndPing establishes a WebSocket connection with ticket, measures latency, and closes.
func (ws *WebSocketClient) ConnectAndPing(ctx context.Context, ticket string) metrics.RequestResult {
	wsURL := fmt.Sprintf("%s/ws?ticket=%s", ws.GatewayURL, ticket)

	dialer := websocket.Dialer{
		HandshakeTimeout: 5 * time.Second,
	}

	startTime := time.Now()
	conn, resp, err := dialer.DialContext(ctx, wsURL, nil)
	duration := time.Since(startTime)

	statusCode := 0
	if resp != nil {
		statusCode = resp.StatusCode
	}

	if err != nil {
		return metrics.RequestResult{
			Endpoint:   "/ws",
			Method:     "WS_CONNECT",
			StatusCode: statusCode,
			Duration:   duration,
			Err:        err,
			Timestamp:  startTime,
		}
	}
	defer conn.Close()

	// Send ping and measure response
	pingStart := time.Now()
	if err := conn.WriteMessage(websocket.PingMessage, []byte("ping")); err != nil {
		return metrics.RequestResult{
			Endpoint:   "/ws",
			Method:     "WS_PING",
			StatusCode: 101,
			Duration:   time.Since(pingStart),
			Err:        err,
			Timestamp:  pingStart,
		}
	}

	return metrics.RequestResult{
		Endpoint:   "/ws",
		Method:     "WS_CONNECT",
		StatusCode: 101, // Switching Protocols (Successful WS handshake)
		Duration:   duration,
		Err:        nil,
		Timestamp:  startTime,
	}
}
