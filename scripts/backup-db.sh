#!/usr/bin/env bash
# Nightly Postgres backup: dumps the running `postgres` container's database,
# compresses it, and uploads it to S3. Intended to run via host cron, e.g.:
#
#   0 3 * * * cd /opt/shopkeeper && ./scripts/backup-db.sh >> /var/log/shopkeeper-backup.log 2>&1
#
# Requires (all already true on the recommended EC2 deployment - see
# database/backup-restore.md):
#   - This script run from the same directory as docker/docker-compose.yml, with
#     the stack already up (`docker compose ... ps` should show `postgres` healthy).
#   - The AWS CLI installed on the host.
#   - AWS credentials available via the standard chain - on EC2 this should be an
#     attached IAM instance role scoped to s3:PutObject on BACKUP_S3_BUCKET only,
#     not a static access key. Never put AWS keys in this script or its env file.
#   - BACKUP_S3_BUCKET set (e.g. in the same .env docker compose already reads).
#
# Retention is handled by an S3 lifecycle rule on the bucket/prefix, not by this
# script - simpler and more reliable than home-grown pruning logic. See
# database/backup-restore.md for the rule to set up.

set -euo pipefail

: "${BACKUP_S3_BUCKET:?BACKUP_S3_BUCKET must be set - see .env.example}"
POSTGRES_USER="${POSTGRES_USER:-shopkeeper}"
POSTGRES_DB="${POSTGRES_DB:-shopkeeper}"
COMPOSE_FILES=(-f docker/docker-compose.yml -f docker/docker-compose.prod.yml)

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DUMP_FILE="shopkeeper-${TIMESTAMP}.sql.gz"
TMP_PATH="/tmp/${DUMP_FILE}"

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Starting backup: ${DUMP_FILE}"

docker compose "${COMPOSE_FILES[@]}" exec -T postgres \
  pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" --no-owner --no-privileges \
  | gzip > "${TMP_PATH}"

DUMP_SIZE=$(du -h "${TMP_PATH}" | cut -f1)
echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Dump complete (${DUMP_SIZE}), uploading to s3://${BACKUP_S3_BUCKET}/"

aws s3 cp "${TMP_PATH}" "s3://${BACKUP_S3_BUCKET}/${DUMP_FILE}" --only-show-errors

rm -f "${TMP_PATH}"

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup uploaded: s3://${BACKUP_S3_BUCKET}/${DUMP_FILE}"
