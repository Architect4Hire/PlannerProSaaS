using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlannerPro.Access.Core.Managers.Models.Domain;

namespace PlannerPro.Access.Core.Data.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.HasKey(i => i.Id);

        // Deliberately global-unique, not (TenantId, Token) — see the XML doc on Invitation.Token.
        builder.HasIndex(i => i.Token).IsUnique();

        // Not unique: a tenant can re-invite the same email after a prior invitation was
        // revoked/expired, so this is a lookup index, not a constraint.
        builder.HasIndex(i => new { i.TenantId, i.Email });

        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Token).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .HasPrincipalKey(t => t.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
