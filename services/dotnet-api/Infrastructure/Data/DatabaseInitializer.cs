using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LogisticServer.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task MigrateAndInitializeAllAsync(CoreDbContext db, ILogger logger)
    {
        logger.LogInformation("Starting EF Core database migration for schema 'core'...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Core schema EF Core migrations applied successfully.");

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        logger.LogInformation("Applying schemas 'dispatch', 'tracking', partitions, and roles from schema specification...");

        const string initScript = @"
-- =====================================================================
-- Extensions & Schemas
-- =====================================================================
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS dispatch;
CREATE SCHEMA IF NOT EXISTS tracking;

-- =====================================================================
-- SCHEMA: dispatch (owned by go-dispatch)
-- =====================================================================
CREATE TABLE IF NOT EXISTS dispatch.dispatch_orders (
    order_id             uuid PRIMARY KEY,
    state                text        NOT NULL DEFAULT 'Searching'
                         CHECK (state IN ('Searching','Offered','Assigned',
                                          'Cancelled','Unassigned','Completed')),
    pickup_location      geography(Point, 4326) NOT NULL,
    vehicle_type         text,
    priority             smallint    NOT NULL DEFAULT 0,
    attempt_count        integer     NOT NULL DEFAULT 0,
    search_radius_m      integer     NOT NULL,
    current_offer_id     uuid,
    assigned_courier_id  uuid,
    version              integer     NOT NULL DEFAULT 1,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_dispatch_orders_state ON dispatch.dispatch_orders (state)
    WHERE state IN ('Searching','Offered');

CREATE TABLE IF NOT EXISTS dispatch.offers (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id      uuid        NOT NULL REFERENCES dispatch.dispatch_orders(order_id) ON DELETE CASCADE,
    courier_id    uuid        NOT NULL,
    attempt_no    integer     NOT NULL,
    status        text        NOT NULL DEFAULT 'Pending'
                  CHECK (status IN ('Pending','Accepted','Rejected','Expired','Cancelled')),
    score         numeric(10,4),
    offered_at    timestamptz NOT NULL DEFAULT now(),
    expires_at    timestamptz NOT NULL,
    responded_at  timestamptz,
    UNIQUE (order_id, courier_id, attempt_no)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_offers_one_pending_per_order   ON dispatch.offers (order_id)   WHERE status = 'Pending';
CREATE UNIQUE INDEX IF NOT EXISTS ux_offers_one_pending_per_courier ON dispatch.offers (courier_id) WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_offers_pending_expiry ON dispatch.offers (expires_at) WHERE status = 'Pending';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_dispatch_current_offer'
    ) THEN
        ALTER TABLE dispatch.dispatch_orders
            ADD CONSTRAINT fk_dispatch_current_offer
            FOREIGN KEY (current_offer_id) REFERENCES dispatch.offers(id)
            DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS dispatch.dispatch_transitions (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id    uuid        NOT NULL REFERENCES dispatch.dispatch_orders(order_id) ON DELETE CASCADE,
    from_state  text,
    to_state    text        NOT NULL,
    trigger     text        NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_dispatch_transitions_order ON dispatch.dispatch_transitions (order_id, occurred_at);

CREATE TABLE IF NOT EXISTS dispatch.courier_profiles (
    courier_id    uuid PRIMARY KEY,
    status        text        NOT NULL,
    vehicle_type  text,
    rating        numeric(3,2) NOT NULL DEFAULT 5.00,
    updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_courier_profiles_status ON dispatch.courier_profiles (status);

CREATE TABLE IF NOT EXISTS dispatch.dispatch_config (
    key         text PRIMARY KEY,
    value       text        NOT NULL,
    updated_at  timestamptz NOT NULL DEFAULT now()
);

INSERT INTO dispatch.dispatch_config (key, value) VALUES
    ('offer_ttl_seconds',        '15'),
    ('initial_radius_m',         '2000'),
    ('radius_step_m',            '1000'),
    ('max_radius_m',             '8000'),
    ('max_attempts',             '10'),
    ('geo_result_limit',         '10')
ON CONFLICT (key) DO NOTHING;

CREATE TABLE IF NOT EXISTS dispatch.outbox_messages (
    id              uuid PRIMARY KEY,
    aggregate_type  text        NOT NULL,
    aggregate_id    uuid        NOT NULL,
    topic           text        NOT NULL,
    message_key     text        NOT NULL,
    event_type      text        NOT NULL,
    event_version   integer     NOT NULL DEFAULT 1,
    payload         jsonb       NOT NULL,
    headers         jsonb       NOT NULL DEFAULT '{}'::jsonb,
    created_at      timestamptz NOT NULL DEFAULT now(),
    published_at    timestamptz
);

CREATE INDEX IF NOT EXISTS ix_dispatch_outbox_unpublished ON dispatch.outbox_messages (created_at)
    WHERE published_at IS NULL;

CREATE TABLE IF NOT EXISTS dispatch.processed_events (
    event_id       uuid        NOT NULL,
    consumer_name  text        NOT NULL,
    processed_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (event_id, consumer_name)
);

-- =====================================================================
-- SCHEMA: tracking (owned by go-ingest and location consumers)
-- =====================================================================
CREATE TABLE IF NOT EXISTS tracking.courier_location_history (
    courier_id   uuid        NOT NULL,
    recorded_at  timestamptz NOT NULL,
    location     geography(Point, 4326) NOT NULL,
    speed_kmh    real,
    heading      real,
    accuracy_m   real,
    order_id     uuid,
    PRIMARY KEY (courier_id, recorded_at)
) PARTITION BY RANGE (recorded_at);

CREATE INDEX IF NOT EXISTS brin_location_recorded_at ON tracking.courier_location_history USING brin (recorded_at);

CREATE OR REPLACE FUNCTION tracking.create_daily_partition(p_day date)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    part_name text := format('courier_location_history_%s', to_char(p_day, 'YYYYMMDD'));
BEGIN
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS tracking.%I PARTITION OF tracking.courier_location_history
         FOR VALUES FROM (%L) TO (%L)',
        part_name, p_day::timestamptz, (p_day + 1)::timestamptz);
END $$;

-- Pre-create daily partitions for today, tomorrow, and day after tomorrow
SELECT tracking.create_daily_partition(CURRENT_DATE);
SELECT tracking.create_daily_partition((CURRENT_DATE + 1)::date);
SELECT tracking.create_daily_partition((CURRENT_DATE + 2)::date);

CREATE TABLE IF NOT EXISTS tracking.courier_last_location (
    courier_id   uuid PRIMARY KEY,
    location     geography(Point, 4326) NOT NULL,
    recorded_at  timestamptz NOT NULL,
    status       text,
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_last_location_geo ON tracking.courier_last_location USING gist (location);

CREATE TABLE IF NOT EXISTS tracking.processed_events (
    event_id       uuid        NOT NULL,
    consumer_name  text        NOT NULL,
    processed_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (event_id, consumer_name)
);

-- =====================================================================
-- Ownership & Service Roles
-- =====================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'svc_core')     THEN CREATE ROLE svc_core     NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'svc_dispatch') THEN CREATE ROLE svc_dispatch NOLOGIN; END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'svc_tracking') THEN CREATE ROLE svc_tracking NOLOGIN; END IF;
END $$;

GRANT USAGE ON SCHEMA core     TO svc_core;     GRANT ALL ON ALL TABLES IN SCHEMA core     TO svc_core;
GRANT USAGE ON SCHEMA dispatch TO svc_dispatch; GRANT ALL ON ALL TABLES IN SCHEMA dispatch TO svc_dispatch;
GRANT USAGE ON SCHEMA tracking TO svc_tracking; GRANT ALL ON ALL TABLES IN SCHEMA tracking TO svc_tracking;
ALTER DEFAULT PRIVILEGES IN SCHEMA core     GRANT ALL ON TABLES TO svc_core;
ALTER DEFAULT PRIVILEGES IN SCHEMA dispatch GRANT ALL ON TABLES TO svc_dispatch;
ALTER DEFAULT PRIVILEGES IN SCHEMA tracking GRANT ALL ON TABLES TO svc_tracking;
GRANT EXECUTE ON FUNCTION tracking.create_daily_partition(date) TO svc_tracking;
";

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = initScript;
            await cmd.ExecuteNonQueryAsync();
        }

        // Seed default admin account if not already present
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole != null)
        {
            var adminExists = await db.UserRoles.AnyAsync(ur => ur.RoleId == adminRole.Id);
            if (!adminExists)
            {
                var adminUser = new Domain.Entities.User
                {
                    Id = Guid.NewGuid(),
                    Email = "admin@logistic.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!", 11),
                    FullName = "Platform Administrator",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Users.Add(adminUser);
                db.UserRoles.Add(new Domain.Entities.UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded default platform admin: admin@logistic.local (password: Admin1234!)");
            }
        }

        // Verification & Reporting
        await VerifyDatabaseAsync(conn, logger);
    }

    public static async Task VerifyDatabaseAsync(System.Data.Common.DbConnection conn, ILogger logger)
    {
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        // PostGIS version check
        await using (var versionCmd = conn.CreateCommand())
        {
            versionCmd.CommandText = "SELECT PostGIS_Full_Version();";
            try
            {
                var postgisVersion = await versionCmd.ExecuteScalarAsync();
                logger.LogInformation("PostGIS verified: {Version}", postgisVersion);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not query PostGIS version.");
            }
        }

        // Table verification across all 3 schemas
        await using (var tableCmd = conn.CreateCommand())
        {
            tableCmd.CommandText = @"
                SELECT table_schema, table_name 
                FROM information_schema.tables 
                WHERE table_schema IN ('core', 'dispatch', 'tracking') 
                ORDER BY table_schema, table_name;";

            await using var reader = await tableCmd.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
            }

            logger.LogInformation("Database tables verified ({Count} total):", tables.Count);
            foreach (var tbl in tables)
            {
                logger.LogInformation("  - {Table}", tbl);
            }
        }
    }
}

