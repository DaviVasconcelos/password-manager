using Microsoft.EntityFrameworkCore;

namespace PasswordManager.Infrastructure.Persistence;

public class VaultDbContext : DbContext
{
    public DbSet<VaultRecord> Vaults => Set<VaultRecord>();

    public VaultDbContext(DbContextOptions<VaultDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<VaultRecord>();

        entity.ToTable("VaultStore");
        entity.HasKey(v => v.Id);
        entity.Property(v => v.SchemaVersion).IsRequired();
        entity.Property(v => v.Salt).IsRequired();
        entity.Property(v => v.EncryptedBlob).IsRequired();
        entity.Property(v => v.UpdatedAt).IsRequired();
    }
}
