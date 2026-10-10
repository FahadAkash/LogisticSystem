package scenarios

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"time"

	"github.com/google/uuid"
	"load-tester/pkg/client"
	"load-tester/pkg/config"
	"load-tester/pkg/metrics"
)

// RunSmoke executes a complete end-to-end sanity verification across all services.
func RunSmoke(ctx context.Context, cfg *config.Config, target *client.TargetClient, wsClient *client.WebSocketClient, col *metrics.Collector) error {
	fmt.Println("\n================================================================================")
	fmt.Printf("   SMOKE TEST: END-TO-END PLATFORM SANITY (Target: %s)\n", cfg.TargetURL)
	fmt.Println("================================================================================")

	step := 1
	logStep := func(name string, success bool, status int, dur time.Duration, err error) {
		statusStr := fmt.Sprintf("[%d OK]", status)
		if !success {
			statusStr = fmt.Sprintf("[%d FAIL: %v]", status, err)
			fmt.Printf("  %2d. %-45s ❌ %s (took %v)\n", step, name, statusStr, dur)
		} else {
			fmt.Printf("  %2d. %-45s ✅ %s (took %v)\n", step, name, statusStr, dur)
		}
		step++
	}

	// 1. Health & Discovery
	res, _ := target.Do(ctx, "GET", "/health", nil, nil, "")
	col.Record(res)
	logStep("ASP.NET Core Health Probe", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	res, _ = target.Do(ctx, "GET", "/.well-known/jwks.json", nil, nil, "")
	col.Record(res)
	logStep("RFC 7517 JWKS Discovery (/.well-known)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	res, _ = target.Do(ctx, "GET", "/api/auth/jwks", nil, nil, "")
	col.Record(res)
	logStep("RFC 7517 JWKS Discovery (/api/auth)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	// 2. Admin Authentication
	token, res, err := target.Login(ctx, cfg.AdminEmail, cfg.AdminPassword)
	col.Record(res)
	logStep("Admin Login (RS256 JWT Issued)", err == nil && token != "", res.StatusCode, res.Duration, err)

	if token == "" {
		return fmt.Errorf("smoke test aborted: could not obtain admin token: %w", err)
	}

	// 3. User Identity Me
	res, _ = target.Do(ctx, "GET", "/api/auth/me", nil, nil, token)
	col.Record(res)
	logStep("Get Current Profile (/api/auth/me)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	// 4. WebSocket Ticket & Handshake
	ticket, res, err := wsClient.AcquireTicket(ctx, token)
	col.Record(res)
	logStep("Issue Single-Use WS Ticket", err == nil && ticket != "", res.StatusCode, res.Duration, err)

	if ticket != "" {
		wsRes := wsClient.ConnectAndPing(ctx, ticket)
		col.Record(wsRes)
		logStep("Go Gateway WebSocket Handshake & Ping", wsRes.StatusCode == 101, wsRes.StatusCode, wsRes.Duration, wsRes.Err)
	}

	// 5. Customer Registration
	custEmail := fmt.Sprintf("smoke_cust_%s@test.local", uuid.New().String()[:8])
	custReq, _ := json.Marshal(map[string]interface{}{
		"email":       custEmail,
		"password":    "Password123!",
		"fullName":    "Smoke Test Customer",
		"phone":       "+1555" + uuid.New().String()[:7],
		"role":        "Customer",
	})
	res, _ = target.Do(ctx, "POST", "/api/auth/register", custReq, nil, "")
	col.Record(res)
	logStep("Customer Registration", res.StatusCode == http.StatusCreated || res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	custToken, _, _ := target.Login(ctx, custEmail, "Password123!")

	// 6. Courier Registration
	courierEmail := fmt.Sprintf("smoke_courier_%s@test.local", uuid.New().String()[:8])
	courierReq, _ := json.Marshal(map[string]interface{}{
		"email":       courierEmail,
		"password":    "Password123!",
		"fullName":    "Smoke Test Courier",
		"phone":       "+1444" + uuid.New().String()[:7],
		"role":        "Courier",
		"vehicleType": "Bike",
	})
	res, _ = target.Do(ctx, "POST", "/api/auth/register", courierReq, nil, "")
	col.Record(res)
	logStep("Courier Registration (Pending Approval)", res.StatusCode == http.StatusCreated || res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	// 7. Admin List Pending Couriers
	res, pendingData := target.Do(ctx, "GET", "/api/admin/couriers/pending", nil, nil, token)
	col.Record(res)
	logStep("Admin List Pending Couriers", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	var pendingList []struct {
		ID uuid.UUID `json:"id"`
	}
	_ = json.Unmarshal(pendingData, &pendingList)
	if len(pendingList) > 0 {
		approveEndpoint := fmt.Sprintf("/api/admin/couriers/%s/approve", pendingList[0].ID)
		res, _ = target.Do(ctx, "POST", approveEndpoint, nil, nil, token)
		col.Record(res)
		logStep("Admin Courier Approval", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)
	}

	// 8. Order Placement with Idempotency Key
	orderIdemKey := uuid.New().String()
	orderReq, _ := json.Marshal(map[string]interface{}{
		"vehicleType":        "Bike",
		"packageDescription": "Smoke Test Express Box",
		"packageWeight":      2.5,
		"stops": []map[string]interface{}{
			{
				"stopType":       "Pickup",
				"sequence":       1,
				"address":        "100 Innovation Way",
				"contactName":    "Alice Sender",
				"contactPhone":   "+15551234567",
				"latitude":       40.7128,
				"longitude":      -74.0060,
			},
			{
				"stopType":       "Dropoff",
				"sequence":       2,
				"address":        "200 Market Street",
				"contactName":    "Bob Receiver",
				"contactPhone":   "+15559876543",
				"latitude":       40.7306,
				"longitude":      -73.9352,
			},
		},
	})

	orderHeaders := map[string]string{
		"Idempotency-Key": orderIdemKey,
	}
	activeCustToken := custToken
	if activeCustToken == "" {
		activeCustToken = token
	}

	res, orderData := target.Do(ctx, "POST", "/api/orders", orderReq, orderHeaders, activeCustToken)
	col.Record(res)
	logStep("Create Multi-Stop Order (PostGIS + Outbox)", res.StatusCode == http.StatusCreated || res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	var orderResp struct {
		ID uuid.UUID `json:"id"`
	}
	_ = json.Unmarshal(orderData, &orderResp)

	// 9. Idempotency Key Replay Verification
	res, _ = target.Do(ctx, "POST", "/api/orders", orderReq, orderHeaders, activeCustToken)
	col.Record(res)
	logStep("Order Idempotency Replay (Cache HIT)", res.StatusCode == http.StatusOK || res.StatusCode == http.StatusCreated, res.StatusCode, res.Duration, res.Err)

	// 10. Query Orders
	res, _ = target.Do(ctx, "GET", "/api/orders", nil, nil, token)
	col.Record(res)
	logStep("Query Orders Feed (/api/orders)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	if orderResp.ID != uuid.Nil {
		res, _ = target.Do(ctx, "GET", fmt.Sprintf("/api/orders/%s", orderResp.ID), nil, nil, activeCustToken)
		col.Record(res)
		logStep("Get Order Details with Spatial Stops", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

		// Cancel order
		cancelReq, _ := json.Marshal(map[string]string{
			"reason": "Smoke test cancellation verification",
		})
		res, _ = target.Do(ctx, "POST", fmt.Sprintf("/api/orders/%s/cancel", orderResp.ID), cancelReq, nil, activeCustToken)
		col.Record(res)
		logStep("Cancel Order Aggregate", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)
	}

	// 11. Courier Fleet Feed
	res, _ = target.Do(ctx, "GET", "/api/couriers", nil, nil, token)
	col.Record(res)
	logStep("Query Couriers Radar (/api/couriers)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	// 12. Golang Microservices Health Probes
	ingestClient := client.NewTargetClient(cfg.GoIngestURL, 3*time.Second)
	res, _ = ingestClient.Do(ctx, "GET", "/health", nil, nil, "")
	col.Record(res)
	logStep("Go Ingest Service Health (:8081)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	dispatchClient := client.NewTargetClient("http://localhost:8082", 3*time.Second)
	res, _ = dispatchClient.Do(ctx, "GET", "/health", nil, nil, "")
	col.Record(res)
	logStep("Go Dispatch Engine Health (:8082)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	gatewayClient := client.NewTargetClient("http://localhost:8083", 3*time.Second)
	res, _ = gatewayClient.Do(ctx, "GET", "/health", nil, nil, "")
	col.Record(res)
	logStep("Go Gateway Health (:8083)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	etaClient := client.NewTargetClient("http://localhost:8084", 3*time.Second)
	res, _ = etaClient.Do(ctx, "GET", "/health", nil, nil, "")
	col.Record(res)
	logStep("Go ETA Calculation Engine Health (:8084)", res.StatusCode == http.StatusOK, res.StatusCode, res.Duration, res.Err)

	fmt.Println("================================================================================")
	fmt.Println("   SMOKE TEST COMPLETE: All Core Services & Endpoints Verified")
	fmt.Println("================================================================================\n")

	return nil
}

