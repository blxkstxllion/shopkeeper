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
2. **Scope an IAM identity to only what the backup script needs, write-only:**
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
   need permission to read, list, or delete them, so a compromised host can't
   read customer data out of old backups or erase your recovery path. On an
   actual EC2 host, attach this as an instance role (no long-lived key on
   disk at all). **This app's production host is a Hetzner VPS, not EC2** -
   there's no instance-metadata mechanism to attach a role to, so instead
   this is a static IAM user access key, stored in `/root/.aws/credentials`
   under a named profile (not `default`, so it's never picked up by accident)
   with `chmod 600`/`chmod 700` on the file and its containing directory.
   The write-only scoping is what actually matters for the compromised-host
   threat model; a static key achieves the same property, it's just a
   different attachment mechanism than an instance role.
3. **Set an S3 lifecycle rule** on the bucket (or the backup prefix) to expire
   objects after however long you want to retain them - e.g. 30 days. This
   replaces any pruning logic in the script itself; S3 lifecycle rules are
   more reliable than a script remembering to delete old files.
4. **Install the AWS CLI** on the host if it isn't already present.
5. **Set `BACKUP_S3_BUCKET`** in the same `.env` the Docker Compose stack
   already reads (see `.env.example`), and set `AWS_PROFILE` to the named
   profile from step 2 wherever the script runs (the cron entry itself, if
   using a static key under a non-default profile rather than an instance
   role).
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

**Last verified: 2026-09-30.** Full chain tested end-to-end against the real
production host and a genuine backup, not a synthetic one:

- Ran the actual nightly backup mechanism manually (`./scripts/backup-db.sh`
  with the real cron-configured `AWS_PROFILE`/`BACKUP_S3_BUCKET`) against the
  live production database and confirmed the object landed in S3.
- Confirmed the write-only IAM credential genuinely cannot list or read
  objects back (`AccessDenied` on `ListBucket`/`GetObject`) - the
  compromised-host threat model this design relies on actually holds.
- Pulled that backup back down using a separate, temporary read-scoped
  credential (created solely for this test, deleted immediately after -
  never stored on the production host) and restored it into an isolated
  throwaway `postgres:16.15` container (matching production's exact version),
  never touching the live database.
- Verified restored row counts matched production exactly at the time of the
  backup: 4 businesses, 5 sales, 9 sale items, 8 products, 4 users. Spot-
  checked `Sales` financial columns (`Subtotal`/`TaxAmount`/`Total`/
  `TotalCost`/`GrossProfit`) for correct decimal precision and `BusinessId`
  presence post-restore.
- Verified schema fidelity, not just row data: 95 indexes and 54 foreign key
  constraints in the restored database, both exact matches against
  production's counts.
- **RTO**: the SQL restore itself (`psql < dump.sql`) took 0.4s at current
  data volume (a ~34KB dump). Full cold-start restore procedure (download
  from S3 + spin up a fresh Postgres container, including a one-time image
  pull since `postgres:16.15` wasn't already cached on the test host + the
  restore itself) took ~3 minutes; the image pull dominated that number and
  wouldn't recur with a warm image cache or an already-running container.
  Re-verify this number as the dataset grows - a `pg_dump`/`psql` restore is
  roughly linear in data size, and this was measured against a very small
  early-stage dataset.
- **RPO**: bounded by the nightly cron schedule (`0 3 * * *`) - worst case,
  up to 24 hours of data loss if the host is lost right before a scheduled
  backup. If that's ever not acceptable at higher transaction volume,
  shortening the interval is a one-line cron change; there's no architectural
  blocker.
- Found and fixed a real bug in the process of running this: `backup-db.sh`
  never passed `--env-file .env` to its `docker compose` invocation, the same
  "Compose infers its project directory from the `-f` paths, not the cwd"
  issue `staging.yml`/`production.yml` had already been fixed for - so the
  very first real run failed outright with missing-env-var errors from
  Compose interpolation, never even reaching `pg_dump`. Confirms the point of
  this whole exercise: the mechanism looked correct by inspection and would
  not have been caught without an actual end-to-end run.
