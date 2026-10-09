using Microsoft.EntityFrameworkCore;
using LogisticServer.Domain.Entities;

namespace LogisticServer.Infrastructure.Data;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Courier> Couriers => Set<Courier>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStop> OrderStops => Set<OrderStop>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Core schema ownership
        modelBuilder.HasDefaultSchema("core");

        // PostgreSQL extensions
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("pgcrypto");

        // 1. Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.FullName).HasColumnName("full_name").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue("Active").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.Email, "ux_users_email")
                  .IsUnique()
                  .HasFilter("deleted_at IS NULL");

            entity.HasIndex(e => e.Phone, "ux_users_phone")
                  .IsUnique()
                  .HasFilter("phone IS NOT NULL AND deleted_at IS NULL");
        });

        // 2. Roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Dispatcher" },
                new Role { Id = 3, Name = "Courier" },
                new Role { Id = 4, Name = "Customer" }
            );
        });

        // 3. UserRoles
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(e => new { e.UserId, e.RoleId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. RefreshTokens
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedBy).HasColumnName("replaced_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId, "ix_refresh_tokens_user");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReplacedByToken)
                  .WithMany()
                  .HasForeignKey(e => e.ReplacedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. Customers
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CompanyName).HasColumnName("company_name");
            entity.Property(e => e.DefaultAddress).HasColumnName("default_address");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasOne(e => e.User)
                  .WithOne(u => u.Customer)
                  .HasForeignKey<Customer>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 6. Vehicles
        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("vehicles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.PlateNo).HasColumnName("plate_no").IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.CapacityWeight).HasColumnName("capacity_weight").HasPrecision(8, 2);
            entity.Property(e => e.CapacityVolume).HasColumnName("capacity_volume").HasPrecision(8, 2);
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue("Active").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            entity.HasIndex(e => e.PlateNo).IsUnique();
        });

        // 7. Couriers
        modelBuilder.Entity<Courier>(entity =>
        {
            entity.ToTable("couriers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VehicleId).HasColumnName("vehicle_id");
            entity.Property(e => e.LicenseNo).HasColumnName("license_no").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue("PendingApproval").IsRequired();
            entity.Property(e => e.Rating).HasColumnName("rating").HasPrecision(3, 2).HasDefaultValue(5.00m);
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.Version).HasColumnName("version").HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.VehicleId, "ux_couriers_vehicle")
                  .IsUnique()
                  .HasFilter("vehicle_id IS NOT NULL AND deleted_at IS NULL");
            entity.HasIndex(e => e.Status, "ix_couriers_status")
                  .HasFilter("deleted_at IS NULL");

            entity.HasOne(e => e.User)
                  .WithOne(u => u.Courier)
                  .HasForeignKey<Courier>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Vehicle)
                  .WithMany(v => v.Couriers)
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 8. Orders
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrderNo).HasColumnName("order_no").IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue("Created").IsRequired();
            entity.Property(e => e.Priority).HasColumnName("priority").HasDefaultValue((short)0);
            entity.Property(e => e.VehicleType).HasColumnName("vehicle_type");
            entity.Property(e => e.PackageDescription).HasColumnName("package_description");
            entity.Property(e => e.PackageWeight).HasColumnName("package_weight").HasPrecision(8, 2);
            entity.Property(e => e.RequestedPickupAt).HasColumnName("requested_pickup_at");
            entity.Property(e => e.AssignedCourierId).HasColumnName("assigned_courier_id");
            entity.Property(e => e.CancelledReason).HasColumnName("cancelled_reason");
            entity.Property(e => e.CancelledBy).HasColumnName("cancelled_by");
            entity.Property(e => e.Version).HasColumnName("version").HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(e => e.OrderNo).IsUnique();
            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "ix_orders_customer_created");
            entity.HasIndex(e => e.AssignedCourierId, "ix_orders_courier")
                  .HasFilter("assigned_courier_id IS NOT NULL");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "ix_orders_active_status")
                  .HasFilter("status IN ('Created','Searching','Offered','Assigned','PickedUp')");

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Orders)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssignedCourier)
                  .WithMany(c => c.AssignedOrders)
                  .HasForeignKey(e => e.AssignedCourierId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CancelledByUser)
                  .WithMany()
                  .HasForeignKey(e => e.CancelledBy)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 9. OrderStops
        modelBuilder.Entity<OrderStop>(entity =>
        {
            entity.ToTable("order_stops");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Sequence).HasColumnName("sequence");
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Address).HasColumnName("address").IsRequired();
            entity.Property(e => e.Location).HasColumnName("location").HasColumnType("geography(Point, 4326)").IsRequired();
            entity.Property(e => e.ContactName).HasColumnName("contact_name");
            entity.Property(e => e.ContactPhone).HasColumnName("contact_phone");
            entity.Property(e => e.ArrivedAt).HasColumnName("arrived_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(e => new { e.OrderId, e.Sequence }).IsUnique();
            entity.HasIndex(e => e.Location, "ix_order_stops_location").HasMethod("gist");

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Stops)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 10. OrderStatusHistory
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("order_status_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.FromStatus).HasColumnName("from_status");
            entity.Property(e => e.ToStatus).HasColumnName("to_status").IsRequired();
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ActorType).HasColumnName("actor_type").IsRequired();
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.OrderId, e.OccurredAt }, "ix_order_history_order");

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.StatusHistories)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. Assignments
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.CourierId).HasColumnName("courier_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasIndex(e => e.OrderId, "ux_assignments_active_order")
                  .IsUnique()
                  .HasFilter("status IN ('Assigned','PickedUp')");
            entity.HasIndex(e => new { e.CourierId, e.Status }, "ix_assignments_courier");

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Assignments)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Courier)
                  .WithMany(c => c.Assignments)
                  .HasForeignKey(e => e.CourierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 12. IdempotencyKeys
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.ToTable("idempotency_keys");
            entity.HasKey(e => new { e.UserId, e.Key });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.RequestHash).HasColumnName("request_hash").IsRequired();
            entity.Property(e => e.ResponseStatus).HasColumnName("response_status");
            entity.Property(e => e.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");

            entity.HasIndex(e => e.ExpiresAt, "ix_idempotency_expires");
        });

        // 13. OutboxMessages
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AggregateType).HasColumnName("aggregate_type").IsRequired();
            entity.Property(e => e.AggregateId).HasColumnName("aggregate_id");
            entity.Property(e => e.Topic).HasColumnName("topic").IsRequired();
            entity.Property(e => e.MessageKey).HasColumnName("message_key").IsRequired();
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(e => e.EventVersion).HasColumnName("event_version").HasDefaultValue(1);
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Headers).HasColumnName("headers").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");

            entity.HasIndex(e => e.CreatedAt, "ix_core_outbox_unpublished")
                  .HasFilter("published_at IS NULL");
        });

        // 14. ProcessedEvents
        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("processed_events");
            entity.HasKey(e => new { e.EventId, e.ConsumerName });
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.ConsumerName).HasColumnName("consumer_name");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at").HasDefaultValueSql("now()");
        });
    }
}
