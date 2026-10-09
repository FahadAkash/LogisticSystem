#!/usr/bin/env bash
# ==============================================================================
# Distributed Logistics Platform - Unified Service Stopper (Bash / Linux / Git Bash)
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_FILE="$SCRIPT_DIR/.services.pid"

echo "================================================================================"
echo "   STOPPING DISTRIBUTED LOGISTICS SERVICES"
echo "================================================================================"

stopped=0

if [ -f "$PID_FILE" ]; then
    PIDS=$(cat "$PID_FILE")
    for pid in $PIDS; do
        if kill -0 "$pid" 2>/dev/null; then
            echo "  Killing PID $pid..."
            kill -9 "$pid" 2>/dev/null || true
            stopped=$((stopped + 1))
        fi
    done
    rm -f "$PID_FILE"
fi

# Fallback: check listening ports using lsof / fuser / netstat if available
PORTS=(5229 8081 8082 8083 8084 4200)
for port in "${PORTS[@]}"; do
    if command -v fuser >/dev/null 2>&1; then
        fuser -k -n tcp "$port" 2>/dev/null || true
    fi
done

echo "[+] Terminated $stopped services."
echo "================================================================================"

