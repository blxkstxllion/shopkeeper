# Backups and restore

Nightly backups are the disaster-recovery plan for this app - there's no
multi-region/HA setup, and none is warranted at current scale (see the
architecture assessment). A tested restore path is what actually matters.

## How backups run

[`scripts/backup-db.sh`](../scripts/backup-db.sh) `pg_dump`s the running
`postgres` container, gzips it, and uploads it to S3. It's meant to run from a
host cron entry, from the same directory as `docker/docker-compose.yml`:

```cron
0 3 * * * cd /opt/shopkeeper && ./scripts/backup-db.sh >> /var/log/shopkeeper-backup.log 2>&1
```

No new container or background worker was added for this - a host cron job
calling a plain script is the simplest correct answer for one nightly job on
one server (consistent with this project not adopting Ansible/Terraform/a
worker process for a single scheduled task).

## One-time setup on the host

1. **Create the S3 bucket** (or reuse one) for backups. Keep it private (no
   public access) - it contains full database dumps, including customer data.
2. **Attach an IAM instance role to the EC2 host** (not a static access key)
   scoped to only what the backup script needs:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": "s3:PutObject",
         "Resource": "arn:aws:s3:::YOUR_BACKUP_BUCKET/*"
       }
     ]
   }
   ```
   This is deliberately write-only - the host that creates backups doesn't
   need permission to read or delete them, so a compromised host can't erase
   your recovery path.
3. **Set an S3 lifecycle rule** on the bucket (or the backup prefix) to expire
   objects after however long you want to retain them - e.g. 30 days. This
   replaces any pruning logic in the script itself; S3 lifecycle rules are
   more reliable than a script remembering to delete old files.
4. **Install the AWS CLI** on the host if it isn't already present.
5. **Set `BACKUP_S3_BUCKET`** in the same `.env` the Docker Compose stack
   already reads (see `.env.example`).
6. Add the cron entry above, then trigger one manual run
   (`./scripts/backup-db.sh`) to confirm it actually reaches S3 before trusting
   the schedule.

## Restoring from a backup

**Restoring overwrites the target database - never run this against
production data you still need.** Always restore into a fresh/empty database
first (a local Postgres, or a scratch RDS/EC2 instance) and verify the data
before considering restoring over a live database.

```bash
# 1. Pull the backup down
aws s3 cp s3://YOUR_BACKUP_BUCKET/shopkeeper-<timestamp>.sql.gz ./restore.sql.gz
gunzip restore.sql.gz

# 2. Restore into a target database (must already exist and be empty)
psql -h <host> -U <user> -d <target_db> -f restore.sql
```

If restoring into the same Docker Compose stack (e.g. rebuilding after
catastrophic data loss on a fresh host):

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.prod.yml up -d postgres
gunzip -c shopkeeper-<timestamp>.sql.gz | \
  docker compose -f docker/docker-compose.yml -f docker/docker-compose.prod.yml \
  exec -T postgres psql -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"
```

Then start the rest of the stack (`api`, `frontend`) once you've confirmed the
data looks right.

## Verifying this actually works

A backup mechanism nobody has ever restored from isn't a disaster-recovery
plan, it's a hope. After setting this up, actually do a test restore into a
throwaway local database at least once, and re-verify after any major schema
migration - don't wait for a real incident to find out the dump is subtly
broken.
