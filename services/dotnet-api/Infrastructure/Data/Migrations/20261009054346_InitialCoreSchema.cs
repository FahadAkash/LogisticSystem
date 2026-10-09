using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LogisticServer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "core",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => new { x.user_id, x.key });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    message_key = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    event_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    headers = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                schema: "core",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_name = table.Column<string>(type: "text", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => new { x.event_id, x.consumer_name });
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plate_no = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    capacity_weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    capacity_volume = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "text", nullable: true),
                    default_address = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.ForeignKey(
                        name: "FK_customers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_tokens_replaced_by",
                        column: x => x.replaced_by,
                        principalSchema: "core",
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "core",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "core",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "couriers",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    license_no = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "PendingApproval"),
                    rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 5.00m),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_couriers", x => x.id);
                    table.ForeignKey(
                        name: "FK_couriers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_couriers_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "core",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_no = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Created"),
                    priority = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    vehicle_type = table.Column<string>(type: "text", nullable: true),
                    package_description = table.Column<string>(type: "text", nullable: true),
                    package_weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    requested_pickup_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_courier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_couriers_assigned_courier_id",
                        column: x => x.assigned_courier_id,
                        principalSchema: "core",
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_users_cancelled_by",
                        column: x => x.cancelled_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    courier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_assignments_couriers_courier_id",
                        column: x => x.courier_id,
                        principalSchema: "core",
                        principalTable: "couriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignments_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "core",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_status_history_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "core",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_stops",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<short>(type: "smallint", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<Point>(type: "geography(Point, 4326)", nullable: false),
                    contact_name = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    arrived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_stops", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_stops_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "core",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "roles",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { (short)1, "Admin" },
                    { (short)2, "Dispatcher" },
                    { (short)3, "Courier" },
                    { (short)4, "Customer" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_courier",
                schema: "core",
                table: "assignments",
                columns: new[] { "courier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_assignments_active_order",
                schema: "core",
                table: "assignments",
                column: "order_id",
                unique: true,
                filter: "status IN ('Assigned','PickedUp')");

            migrationBuilder.CreateIndex(
                name: "ix_couriers_status",
                schema: "core",
                table: "couriers",
                column: "status",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_couriers_user_id",
                schema: "core",
                table: "couriers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_couriers_vehicle",
                schema: "core",
                table: "couriers",
                column: "vehicle_id",
                unique: true,
                filter: "vehicle_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_customers_user_id",
                schema: "core",
                table: "customers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_expires",
                schema: "core",
                table: "idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_order_history_order",
                schema: "core",
                table: "order_status_history",
                columns: new[] { "order_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_order_stops_location",
                schema: "core",
                table: "order_stops",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_order_stops_order_id_sequence",
                schema: "core",
                table: "order_stops",
                columns: new[] { "order_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_active_status",
                schema: "core",
                table: "orders",
                columns: new[] { "status", "created_at" },
                filter: "status IN ('Created','Searching','Offered','Assigned','PickedUp')");

            migrationBuilder.CreateIndex(
                name: "IX_orders_cancelled_by",
                schema: "core",
                table: "orders",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "ix_orders_courier",
                schema: "core",
                table: "orders",
                column: "assigned_courier_id",
                filter: "assigned_courier_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_created",
                schema: "core",
                table: "orders",
                columns: new[] { "customer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_no",
                schema: "core",
                table: "orders",
                column: "order_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_core_outbox_unpublished",
                schema: "core",
                table: "outbox_messages",
                column: "created_at",
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_replaced_by",
                schema: "core",
                table: "refresh_tokens",
                column: "replaced_by");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                schema: "core",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user",
                schema: "core",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "core",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "core",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                schema: "core",
                table: "users",
                column: "email",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_users_phone",
                schema: "core",
                table: "users",
                column: "phone",
                unique: true,
                filter: "phone IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_plate_no",
                schema: "core",
                table: "vehicles",
                column: "plate_no",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "core");

            migrationBuilder.DropTable(
                name: "order_status_history",
                schema: "core");

            migrationBuilder.DropTable(
                name: "order_stops",
                schema: "core");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "core");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "core");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "core");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "couriers",
                schema: "core");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "core");

            migrationBuilder.DropTable(
                name: "vehicles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "users",
                schema: "core");
        }
    }
}
