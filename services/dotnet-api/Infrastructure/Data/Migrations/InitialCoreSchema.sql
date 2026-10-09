DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
        CREATE SCHEMA core;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS core."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
        CREATE SCHEMA core;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
        CREATE SCHEMA core;
    END IF;
END $EF$;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
        CREATE SCHEMA core;
    END IF;
END $EF$;

CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE core.idempotency_keys (
    user_id uuid NOT NULL,
    key text NOT NULL,
    request_hash text NOT NULL,
    response_status integer,
    response_body jsonb,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    expires_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_idempotency_keys" PRIMARY KEY (user_id, key)
);

CREATE TABLE core.outbox_messages (
    id uuid NOT NULL,
    aggregate_type text NOT NULL,
    aggregate_id uuid NOT NULL,
    topic text NOT NULL,
    message_key text NOT NULL,
    event_type text NOT NULL,
    event_version integer NOT NULL DEFAULT 1,
    payload jsonb NOT NULL,
    headers jsonb NOT NULL DEFAULT ('{}'::jsonb),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    published_at timestamp with time zone,
    CONSTRAINT "PK_outbox_messages" PRIMARY KEY (id)
);

CREATE TABLE core.processed_events (
    event_id uuid NOT NULL,
    consumer_name text NOT NULL,
    processed_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_processed_events" PRIMARY KEY (event_id, consumer_name)
);

CREATE TABLE core.roles (
    id smallint NOT NULL,
    name text NOT NULL,
    CONSTRAINT "PK_roles" PRIMARY KEY (id)
);

CREATE TABLE core.users (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    email text NOT NULL,
    phone text,
    password_hash text NOT NULL,
    full_name text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    CONSTRAINT "PK_users" PRIMARY KEY (id)
);

CREATE TABLE core.vehicles (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    plate_no text NOT NULL,
    type text NOT NULL,
    capacity_weight numeric(8,2),
    capacity_volume numeric(8,2),
    status text NOT NULL DEFAULT 'Active',
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_vehicles" PRIMARY KEY (id)
);

CREATE TABLE core.customers (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    company_name text,
    default_address text,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    CONSTRAINT "PK_customers" PRIMARY KEY (id),
    CONSTRAINT "FK_customers_users_user_id" FOREIGN KEY (user_id) REFERENCES core.users (id) ON DELETE CASCADE
);

CREATE TABLE core.refresh_tokens (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    token_hash text NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    revoked_at timestamp with time zone,
    replaced_by uuid,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
    CONSTRAINT "FK_refresh_tokens_refresh_tokens_replaced_by" FOREIGN KEY (replaced_by) REFERENCES core.refresh_tokens (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refresh_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES core.users (id) ON DELETE CASCADE
);

CREATE TABLE core.user_roles (
    user_id uuid NOT NULL,
    role_id smallint NOT NULL,
    CONSTRAINT "PK_user_roles" PRIMARY KEY (user_id, role_id),
    CONSTRAINT "FK_user_roles_roles_role_id" FOREIGN KEY (role_id) REFERENCES core.roles (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_user_roles_users_user_id" FOREIGN KEY (user_id) REFERENCES core.users (id) ON DELETE CASCADE
);

CREATE TABLE core.couriers (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    vehicle_id uuid,
    license_no text NOT NULL,
    status text NOT NULL DEFAULT 'PendingApproval',
    rating numeric(3,2) NOT NULL DEFAULT 5.0,
    approved_at timestamp with time zone,
    version integer NOT NULL DEFAULT 1,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    CONSTRAINT "PK_couriers" PRIMARY KEY (id),
    CONSTRAINT "FK_couriers_users_user_id" FOREIGN KEY (user_id) REFERENCES core.users (id) ON DELETE CASCADE,
    CONSTRAINT "FK_couriers_vehicles_vehicle_id" FOREIGN KEY (vehicle_id) REFERENCES core.vehicles (id) ON DELETE SET NULL
);

CREATE TABLE core.orders (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    order_no text NOT NULL,
    customer_id uuid NOT NULL,
    status text NOT NULL DEFAULT 'Created',
    priority smallint NOT NULL DEFAULT 0,
    vehicle_type text,
    package_description text,
    package_weight numeric(8,2),
    requested_pickup_at timestamp with time zone,
    assigned_courier_id uuid,
    cancelled_reason text,
    cancelled_by uuid,
    version integer NOT NULL DEFAULT 1,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    completed_at timestamp with time zone,
    CONSTRAINT "PK_orders" PRIMARY KEY (id),
    CONSTRAINT "FK_orders_couriers_assigned_courier_id" FOREIGN KEY (assigned_courier_id) REFERENCES core.couriers (id) ON DELETE SET NULL,
    CONSTRAINT "FK_orders_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES core.customers (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_orders_users_cancelled_by" FOREIGN KEY (cancelled_by) REFERENCES core.users (id) ON DELETE SET NULL
);

CREATE TABLE core.assignments (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    order_id uuid NOT NULL,
    courier_id uuid NOT NULL,
    status text NOT NULL,
    assigned_at timestamp with time zone NOT NULL,
    completed_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_assignments" PRIMARY KEY (id),
    CONSTRAINT "FK_assignments_couriers_courier_id" FOREIGN KEY (courier_id) REFERENCES core.couriers (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_assignments_orders_order_id" FOREIGN KEY (order_id) REFERENCES core.orders (id) ON DELETE CASCADE
);

CREATE TABLE core.order_status_history (
    id bigint GENERATED ALWAYS AS IDENTITY,
    order_id uuid NOT NULL,
    from_status text,
    to_status text NOT NULL,
    reason text,
    actor_type text NOT NULL,
    actor_id uuid,
    occurred_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_order_status_history" PRIMARY KEY (id),
    CONSTRAINT "FK_order_status_history_orders_order_id" FOREIGN KEY (order_id) REFERENCES core.orders (id) ON DELETE CASCADE
);

CREATE TABLE core.order_stops (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    order_id uuid NOT NULL,
    sequence smallint NOT NULL,
    type text NOT NULL,
    address text NOT NULL,
    location geography(Point, 4326) NOT NULL,
    contact_name text,
    contact_phone text,
    arrived_at timestamp with time zone,
    completed_at timestamp with time zone,
    CONSTRAINT "PK_order_stops" PRIMARY KEY (id),
    CONSTRAINT "FK_order_stops_orders_order_id" FOREIGN KEY (order_id) REFERENCES core.orders (id) ON DELETE CASCADE
);

INSERT INTO core.roles (id, name)
VALUES (1, 'Admin');
INSERT INTO core.roles (id, name)
VALUES (2, 'Dispatcher');
INSERT INTO core.roles (id, name)
VALUES (3, 'Courier');
INSERT INTO core.roles (id, name)
VALUES (4, 'Customer');

CREATE INDEX ix_assignments_courier ON core.assignments (courier_id, status);

CREATE UNIQUE INDEX ux_assignments_active_order ON core.assignments (order_id) WHERE status IN ('Assigned','PickedUp');

CREATE INDEX ix_couriers_status ON core.couriers (status) WHERE deleted_at IS NULL;

CREATE UNIQUE INDEX "IX_couriers_user_id" ON core.couriers (user_id);

CREATE UNIQUE INDEX ux_couriers_vehicle ON core.couriers (vehicle_id) WHERE vehicle_id IS NOT NULL AND deleted_at IS NULL;

CREATE UNIQUE INDEX "IX_customers_user_id" ON core.customers (user_id);

CREATE INDEX ix_idempotency_expires ON core.idempotency_keys (expires_at);

CREATE INDEX ix_order_history_order ON core.order_status_history (order_id, occurred_at);

CREATE INDEX ix_order_stops_location ON core.order_stops USING gist (location);

CREATE UNIQUE INDEX "IX_order_stops_order_id_sequence" ON core.order_stops (order_id, sequence);

CREATE INDEX ix_orders_active_status ON core.orders (status, created_at) WHERE status IN ('Created','Searching','Offered','Assigned','PickedUp');

CREATE INDEX "IX_orders_cancelled_by" ON core.orders (cancelled_by);

CREATE INDEX ix_orders_courier ON core.orders (assigned_courier_id) WHERE assigned_courier_id IS NOT NULL;

CREATE INDEX ix_orders_customer_created ON core.orders (customer_id, created_at);

CREATE UNIQUE INDEX "IX_orders_order_no" ON core.orders (order_no);

CREATE INDEX ix_core_outbox_unpublished ON core.outbox_messages (created_at) WHERE published_at IS NULL;

CREATE INDEX "IX_refresh_tokens_replaced_by" ON core.refresh_tokens (replaced_by);

CREATE UNIQUE INDEX "IX_refresh_tokens_token_hash" ON core.refresh_tokens (token_hash);

CREATE INDEX ix_refresh_tokens_user ON core.refresh_tokens (user_id);

CREATE UNIQUE INDEX "IX_roles_name" ON core.roles (name);

CREATE INDEX "IX_user_roles_role_id" ON core.user_roles (role_id);

CREATE UNIQUE INDEX ux_users_email ON core.users (email) WHERE deleted_at IS NULL;

CREATE UNIQUE INDEX ux_users_phone ON core.users (phone) WHERE phone IS NOT NULL AND deleted_at IS NULL;

CREATE UNIQUE INDEX "IX_vehicles_plate_no" ON core.vehicles (plate_no);

INSERT INTO core."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009054346_InitialCoreSchema', '10.0.12');

COMMIT;

