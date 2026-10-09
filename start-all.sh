#!/usr/bin/env bash
# ==============================================================================
# Distributed Logistics Platform - Unified Service Runner (Bash / Linux / Git Bash)
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_FILE="$SCRIPT_DIR/.services.pid"
LOG_DIR="$SCRIPT_DIR/logs"
mkdir -p "$LOG_DIR"

# Detect LAN IP
LAN_IP="127.0.0.1"
if command -v hostname >/dev/null 2>&1 && hostname -I >/dev/null 2>&1; then
    LAN_IP=$(hostname -I | awk '{print $1}')
elif command -v ip >/dev/null 2>&1; then
    LAN_IP=$(ip route get 1.1.1.1 2>/dev/null | grep -oP 'src \K\S+' || echo "127.0.0.1")
elif command -v ipconfig >/dev/null 2>&1; then
    LAN_IP=$(ipconfig | grep -i "IPv4" | head -n 1 | awk -F': ' '{print $2}' | tr -d '\r')
fi

echo "================================================================================"
echo "   DISTRIBUTED LOGISTICS & DISPATCH PLATFORM - LAUNCHER"
echo "================================================================================"
echo "  Primary LAN IP Address : $LAN_IP"
echo "  Localhost              : 127.0.0.1 / localhost"
echo ""

# Stop previously running services if PID file exists
if [ -f "$PID_FILE" ]; then
    echo "[!] Existing PID file found. Stopping previous processes..."
    "$SCRIPT_DIR/stop-all.sh" || true
fi

echo "[*] Launching services..."

# 1. dotnet-api (Port 5229)
echo "  -> Starting dotnet-api on :5229..."
(cd "$SCRIPT_DIR/services/dotnet-api" && dotnet run --no-launch-profile --urls "http://0.0.0.0:5229" > "$LOG_DIR/dotnet-api.log" 2>&1) &
PID_API=$!

# 2. dotnet-worker
echo "  -> Starting dotnet-worker..."
(cd "$SCRIPT_DIR/services/dotnet-worker" && dotnet run --no-launch-profile > "$LOG_DIR/dotnet-worker.log" 2>&1) &
PID_WORKER=$!

# 3. go-ingest (Port 8081)
echo "  -> Starting go-ingest on :8081..."
(cd "$SCRIPT_DIR/services/go-ingest" && go run main.go > "$LOG_DIR/go-ingest.log" 2>&1) &
PID_INGEST=$!

# 4. go-dispatch (Port 8082)
echo "  -> Starting go-dispatch on :8082..."
(cd "$SCRIPT_DIR/services/go-dispatch" && go run main.go > "$LOG_DIR/go-dispatch.log" 2>&1) &
PID_DISPATCH=$!

# 5. go-gateway (Port 8083)
echo "  -> Starting go-gateway on :8083..."
(cd "$SCRIPT_DIR/services/go-gateway" && go run main.go > "$LOG_DIR/go-gateway.log" 2>&1) &
PID_GATEWAY=$!

# 6. go-eta (Port 8084)
echo "  -> Starting go-eta on :8084..."
(cd "$SCRIPT_DIR/services/go-eta" && go run main.go > "$LOG_DIR/go-eta.log" 2>&1) &
PID_ETA=$!

# 7. Angular frontend (Port 4200)
if [ "$1" != "--no-frontend" ]; then
    echo "  -> Starting angular-web on :4200..."
    (cd "$SCRIPT_DIR/web/angular" && npm start -- --host 0.0.0.0 --port 4200 > "$LOG_DIR/angular-web.log" 2>&1) &
    PID_FRONTEND=$!
    echo "$PID_API $PID_WORKER $PID_INGEST $PID_DISPATCH $PID_GATEWAY $PID_ETA $PID_FRONTEND" > "$PID_FILE"
else
    echo "$PID_API $PID_WORKER $PID_INGEST $PID_DISPATCH $PID_GATEWAY $PID_ETA" > "$PID_FILE"
fi

echo ""
echo "================================================================================"
echo "   SYSTEM SERVICES CONNECTION & IP DASHBOARD"
echo "================================================================================"
echo "  HOST LAN IP : $LAN_IP"
echo "  LOCALHOST   : 127.0.0.1"
echo "--------------------------------------------------------------------------------"
printf "%-18s %-6s %-28s %-28s\n" "SERVICE" "PORT" "LOCAL URL" "NETWORK LAN URL"
printf "%-18s %-6s %-28s %-28s\n" "-------" "----" "---------" "---------------"
printf "%-18s %-6s %-28s %-28s\n" "dotnet-api" "5229" "http://localhost:5229" "http://$LAN_IP:5229"
printf "%-18s %-6s %-28s %-28s\n" "dotnet-worker" "BG" "(Background Worker)" "(N/A)"
printf "%-18s %-6s %-28s %-28s\n" "go-ingest" "8081" "http://localhost:8081" "http://$LAN_IP:8081"
printf "%-18s %-6s %-28s %-28s\n" "go-dispatch" "8082" "http://localhost:8082" "http://$LAN_IP:8082"
printf "%-18s %-6s %-28s %-28s\n" "go-gateway" "8083" "http://localhost:8083" "http://$LAN_IP:8083"
printf "%-18s %-6s %-28s %-28s\n" "go-eta" "8084" "http://localhost:8084" "http://$LAN_IP:8084"
if [ "$1" != "--no-frontend" ]; then
    printf "%-18s %-6s %-28s %-28s\n" "angular-web" "4200" "http://localhost:4200" "http://$LAN_IP:4200"
fi
echo "--------------------------------------------------------------------------------"
echo "  KEY PLATFORM ENDPOINTS:"
echo "  - Angular App     : http://localhost:4200  |  http://$LAN_IP:4200"
echo "  - OpenAPI Docs    : http://localhost:5229/openapi/v1.json"
echo "  - Public JWKS     : http://localhost:5229/.well-known/jwks.json"
echo "  - Health Checks   : http://localhost:5229/health (API) | :8081/health (Ingest)"
echo "================================================================================"
echo ""
echo "Logs are streaming to ./logs/*.log"
echo "Run ./stop-all.sh to terminate all services."

