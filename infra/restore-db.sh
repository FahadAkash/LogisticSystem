#!/usr/bin/env bash
# ==============================================================================
# PostgreSQL Database Restore Script
# Restores a compressed .sql.gz dump into the running PostgreSQL container.
# ==============================================================================
set -euo pipefail

CONTAINER_NAME="${CONTAINER_NAME:-logistic-postgres}"
DB_NAME="${DB_NAME:-appdb}"
DB_USER="${DB_USER:-postgres}"

if [ $# -eq 0 ]; then
    echo "Usage: $0 <path_to_backup_file.sql.gz>"
    echo "Example: $0 ./backups/appdb_20261010_120000.sql.gz"
    exit 1
fi

BACKUP_FILE="$1"

if [ ! -f "$BACKUP_FILE" ]; then
    echo "[-] Error: Backup file '$BACKUP_FILE' not found!"
    exit 1
fi

echo "=================================================================="
echo " WARNING: Restoring Database from Backup"
echo " Target Container : $CONTAINER_NAME"
echo " Target Database  : $DB_NAME"
echo " Backup File      : $BACKUP_FILE"
echo "=================================================================="

read -p "Are you sure you want to restore? This will overwrite active data! (y/N): " -r CONFIRM
if [[ ! "$CONFIRM" =~ ^[Yy]$ ]]; then
    echo "[*] Restore aborted."
    exit 0
fi

echo "[*] Decompressing and executing restore..."
gunzip -c "$BACKUP_FILE" | docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME"

echo "[+] Database restore completed successfully!"
