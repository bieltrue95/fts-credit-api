using FtsCredit.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FtsCredit.Api.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CreditRequest> CreditRequests => Set<CreditRequest>();
    public DbSet<RiskAnalysis> RiskAnalyses => Set<RiskAnalysis>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Document).HasColumnName("document").IsRequired().HasMaxLength(14);
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.MonthlyIncome).HasColumnName("monthly_income").HasColumnType("numeric(18,2)");
            e.Property(x => x.RiskLevel).HasColumnName("risk_level").HasConversion<string>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Document).IsUnique();
        });

        modelBuilder.Entity<CreditRequest>(e =>
        {
            e.ToTable("credit_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
            e.Property(x => x.Installments).HasColumnName("installments");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.ProductType).HasColumnName("product_type").HasConversion<string>();
            e.Property(x => x.ApprovedLimit).HasColumnName("approved_limit").HasColumnType("numeric(18,2)");
            e.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Customer).WithMany(x => x.CreditRequests).HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<RiskAnalysis>(e =>
        {
            e.ToTable("risk_analyses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RequestId).HasColumnName("request_id");
            e.Property(x => x.Score).HasColumnName("score");
            e.Property(x => x.ApprovedLimit).HasColumnName("approved_limit").HasColumnType("numeric(18,2)");
            e.Property(x => x.RiskLevel).HasColumnName("risk_level").HasConversion<string>();
            e.Property(x => x.AnalysedAt).HasColumnName("analysed_at");
            e.Property(x => x.EngineVersion).HasColumnName("engine_version").HasMaxLength(20);
            e.HasOne<CreditRequest>().WithOne(x => x.RiskAnalysis).HasForeignKey<RiskAnalysis>(x => x.RequestId);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
