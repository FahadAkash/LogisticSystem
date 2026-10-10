#!/usr/bin/env bash
# ==============================================================================
# PostgreSQL Automated Hot Backup Script
# Creates compressed, timestamped database dumps and cleans up old backups.
# ==============================================================================
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-./backups}"
CONTAINER_NAME="${CONTAINER_NAME:-logistic-postgres}"
DB_NAME="${DB_NAME:-appdb}"
DB_USER="${DB_USER:-postgres}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"

mkdir -p "$BACKUP_DIR"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="$BACKUP_DIR/${DB_NAME}_${TIMESTAMP}.sql.gz"

echo "=================================================================="
echo " Starting Hot Database Backup: $DB_NAME"
echo " Container : $CONTAINER_NAME"
echo " Output    : $BACKUP_FILE"
echo "=================================================================="

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "[-] Error: Container '$CONTAINER_NAME' is not running!"
    exit 1
fi

# Execute pg_dump inside container and compress on host
docker exec -t "$CONTAINER_NAME" pg_dump -U "$DB_USER" -d "$DB_NAME" --clean --if-exists | gzip -9 > "$BACKUP_FILE"

SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
echo "[+] Backup created successfully! Size: $SIZE"

# Cleanup backups older than retention window
echo "[*] Cleaning up backups older than $RETENTION_DAYS days..."
find "$BACKUP_DIR" -type f -name "${DB_NAME}_*.sql.gz" -mtime +"$RETENTION_DAYS" -exec rm -f {} \; || true

echo "[+] Done. System is fully protected."
