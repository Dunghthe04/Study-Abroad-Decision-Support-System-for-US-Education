#!/usr/bin/env bash
# Dump the production database to ./backups (keeps the last 14 dumps). Schedule with cron, e.g.:
#   0 3 * * * cd /opt/usas && ./scripts/backup-db.sh >> backups/backup.log 2>&1
set -euo pipefail

set -a; source .env; set +a
mkdir -p backups
FILE="backups/${POSTGRES_DB:-studyabroad}_$(date +%Y%m%d_%H%M%S).sql.gz"

docker compose -f docker-compose.prod.yml --env-file .env exec -T db \
  pg_dump -U "${POSTGRES_USER:-studyabroad}" -d "${POSTGRES_DB:-studyabroad}" | gzip > "$FILE"

ls -1t backups/*.sql.gz | tail -n +15 | xargs -r rm --
echo "Backup written: $FILE"
