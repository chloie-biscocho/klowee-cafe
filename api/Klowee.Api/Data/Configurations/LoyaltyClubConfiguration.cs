using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class LoyaltyClubConfiguration : IEntityTypeConfiguration<LoyaltyClub>
{
    public void Configure(EntityTypeBuilder<LoyaltyClub> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(80);
        builder.HasIndex(c => c.Name).IsUnique().HasFilter(SoftDelete.Filter);
        builder.Property(c => c.Style).HasConversion<string>().HasMaxLength(32);

        builder.HasMany(c => c.Rewards)
            .WithOne(r => r.Club!)
            .HasForeignKey(r => r.ClubId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoyaltyClubRewardConfiguration : IEntityTypeConfiguration<LoyaltyClubReward>
{
    public void Configure(EntityTypeBuilder<LoyaltyClubReward> builder)
    {
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Percent).HasPrecision(5, 2);
        builder.Property(r => r.Label).IsRequired().HasMaxLength(40);
        builder.HasIndex(r => new { r.ClubId, r.Slot }).IsUnique().HasFilter(SoftDelete.Filter);

        builder.HasOne(r => r.AddOn)
            .WithMany()
            .HasForeignKey(r => r.AddOnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoyaltyRedemptionConfiguration : IEntityTypeConfiguration<LoyaltyRedemption>
{
    public void Configure(EntityTypeBuilder<LoyaltyRedemption> builder)
    {
        builder.Property(r => r.Note).HasMaxLength(500);

        // A reward is used at most once per pass round the card.
        builder.HasIndex(r => new { r.CustomerId, r.ClubId, r.Cycle, r.RewardId }).IsUnique().HasFilter(SoftDelete.Filter);

        builder.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Club).WithMany().HasForeignKey(r => r.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Reward).WithMany().HasForeignKey(r => r.RewardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);

        // The line points at its redemption (order_items.loyalty_redemption_id);
        // this is the way back, for "which line used it".
        builder.HasOne(r => r.OrderItem).WithMany().HasForeignKey(r => r.OrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DataMigrationConfiguration : IEntityTypeConfiguration<DataMigration>
{
    public void Configure(EntityTypeBuilder<DataMigration> builder)
    {
        builder.Property(m => m.Name).IsRequired().HasMaxLength(120);
        builder.Property(m => m.Summary).HasMaxLength(4000);
        builder.HasIndex(m => m.Name).IsUnique().HasFilter(SoftDelete.Filter);
    }
}
