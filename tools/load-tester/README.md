# Distributed Logistics Platform — High-Throughput Load & Stress Testing Engine

A dedicated, concurrent benchmarking and chaos detection suite written in Golang to stress test the Distributed Logistics & Real-Time Fleet Dispatch Platform.

Designed to discover the **exact capacity breaking point**, calculate latency percentiles ($p50, p90, p95, p99$), diagnose **which specific microservice or database connection pool fails**, and generate interactive HTML and Markdown reports.

---

## 1. Quick Start (Local IP Testing)

All commands default to local high-speed origin endpoints (`http://localhost:5229`, `http://localhost:8081`, `ws://localhost:8083`) to avoid external Cloudflare edge rate limits.

Navigate to the tool directory:
```powershell
cd f:\coding\server_project\tools\load-tester
```

### A. Smoke Test (Sanity Check)
Validates all 22 REST endpoints, identity tokens, WebSocket handshakes, and microservice health:
```powershell
.\loadtest.exe -mode smoke
```

### B. Standard Concurrent Load Test
Runs a steady-state customer order journey with 100 concurrent users for 1 minute:
```powershell
.\loadtest.exe -mode load -users 100 -duration 1m
```

### C. Stepped Stress Test (Breaking Point Discovery)
Progressively scales concurrent users ($50 \rightarrow 150 \rightarrow 250 \rightarrow 350 \dots 2000+$) in 15-second intervals until degradation or failure:
```powershell
.\loadtest.exe -mode stress -users 50 -max-users 2000 -step-users 100 -step-duration 15s
```

### D. Traffic Spike Shock Test
Blasts instantaneous bursts of 500 simultaneous requests with synchronized barriers to test connection pool recovery:
```powershell
.\loadtest.exe -mode spike -users 500
```

### E. High-Frequency GPS Telematics Flood
Floods `go-ingest` (`POST /api/v1/location`) with high-frequency GPS coordinate pings to stress Redis `couriers:geo`:
```powershell
.\loadtest.exe -mode courier -users 300 -duration 45s
```

### F. WebSocket Concurrency Stress
Holds hundreds of concurrent WebSockets connected to `go-gateway` via single-use tickets:
```powershell
.\loadtest.exe -mode ws -users 500 -duration 30s
```

---

## 2. Testing Specific Network Hosts

To test against your machine's LAN IP (`192.168.0.104`) or custom host:
```powershell
.\loadtest.exe -mode stress -target "http://192.168.0.104:5229" -ingest-url "http://192.168.0.104:8081" -gateway-url "ws://192.168.0.104:8083"
```

To test through the public Cloudflare tunnel:
```powershell
.\loadtest.exe -mode smoke -target "https://bell-por-working-improving.trycloudflare.com"
```

---

## 3. Real-Time Telemetry & Reports

During execution, a live ANSI dashboard updates at 2 Hz:
```
⏳ [VUs:  500] | RPS:  1420.5 | Total:  18500 | OK:  18420 | Err:   80 (0.4%) | P95:  14.2ms | Health: ALL SERVICES HEALTHY
```

Upon completion, two reports are automatically produced:
1. **Interactive HTML Report** (`load_test_report.html`): Self-contained dashboard with interactive Chart.js throughput curves, latency distribution graphs, and microservice status matrix.
2. **Markdown Report** (`STRESS_TEST_REPORT.md`): Formatted GitHub summary with status code tables and breaking point assessments.

---

## 4. Root-Cause Diagnostic Engine

The engine monitors:
- **Cloudflare 524 / 521**: Tunnel rate limits or origin timeouts.
- **HTTP 500**: PostgreSQL connection pool exhaustion or transaction lockups.
- **HTTP 429**: Rate limiting throttles.
- **Connection Refused / Timeouts**: ASP.NET thread pool exhaustion or TCP accept backlog saturation.
- **Independent Watchdog**: Probes `/health` on all services every second to pinpoint the exact service that went down and the concurrent user count when it occurred.

