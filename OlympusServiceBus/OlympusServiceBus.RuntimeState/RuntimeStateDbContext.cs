using Microsoft.EntityFrameworkCore;
using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState;

public class RuntimeStateDbContext : DbContext
{
    public RuntimeStateDbContext(DbContextOptions<RuntimeStateDbContext> options) : base(options)
    {
        
    }

    public DbSet<ContractExecutionStateEntity> ContractExecutionStates => Set<ContractExecutionStateEntity>();
    public DbSet<ContractMessageStateEntity> ContractMessageStates => Set<ContractMessageStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContractExecutionStateEntity>(e =>
        {
            e.ToTable("ContractExecutionState");
            e.HasKey(x => x.ContractId);

            e.Property(x => x.ContractId).IsRequired();
            e.Property(x => x.ContractName).IsRequired().HasMaxLength(200);
            e.Property(x => x.LastRunStatus).HasMaxLength(50);
        });

        modelBuilder.Entity<ContractMessageStateEntity>(e =>
        {
            e.ToTable("ContractMessageState");
            e.HasKey(x => x.Id);

            e.Property(x => x.ContractId).IsRequired();
            e.Property(x => x.ContractName).IsRequired().HasMaxLength(200);
            e.Property(x => x.BusinessKey).IsRequired();
            e.Property(x => x.PayloadHash).IsRequired();

            e.HasIndex(x => new { x.ContractId, x.BusinessKey }).IsUnique();
        });
    }
}