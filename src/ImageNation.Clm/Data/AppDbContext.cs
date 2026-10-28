using ImageNation.Clm.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ImageNation.Clm.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<InsEntity> InsEntities => Set<InsEntity>();
    public DbSet<AgreementType> AgreementTypes => Set<AgreementType>();
    public DbSet<NonSupplierCategory> NonSupplierCategories => Set<NonSupplierCategory>();
    public DbSet<ProjectTitle> Projects => Set<ProjectTitle>();
    public DbSet<DoaPolicy> DoaPolicies => Set<DoaPolicy>();
    public DbSet<FxRate> FxRates => Set<FxRate>();
    public DbSet<ReminderLeadDay> ReminderLeadDays => Set<ReminderLeadDay>();
    public DbSet<ContractNumberSequence> NumberSequences => Set<ContractNumberSequence>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<DocumentVersion> Versions => Set<DocumentVersion>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<Signatory> Signatories => Set<Signatory>();
    public DbSet<Obligation> Obligations => Set<Obligation>();
    public DbSet<RightsWindow> Rights => Set<RightsWindow>();
    public DbSet<CriticalDate> CriticalDates => Set<CriticalDate>();
    public DbSet<RelatedDocument> RelatedDocuments => Set<RelatedDocument>();
    public DbSet<AssignmentEvent> Assignments => Set<AssignmentEvent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notice> Notices => Set<Notice>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var enumProps = builder.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties().Select(property => new
            {
                Entity = entity.ClrType,
                property.Name,
                Type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType
            }))
            .Where(property => property.Type.IsEnum)
            .ToList();
        foreach (var property in enumProps)
        {
            var method = typeof(AppDbContext).GetMethod(nameof(SetEnumString), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(property.Type);
            method.Invoke(null, [builder, property.Entity, property.Name]);
        }

        foreach (var property in builder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)).ToList())
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }

        foreach (var key in builder.Model.GetEntityTypes().SelectMany(entity => entity.GetKeys()).Where(key => key.Properties.Count == 1 && key.Properties[0].ClrType == typeof(Guid)))
        {
            key.Properties[0].ValueGenerated = ValueGenerated.Never;
        }

        builder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        builder.Entity<Contract>().HasIndex(c => c.PublicId).IsUnique();
        builder.Entity<Contract>().HasIndex(c => c.LifecycleStatus);
        builder.Entity<Contract>().HasIndex(c => c.Department);
        builder.Entity<Contract>().HasIndex(c => c.AssignedLawyerId);
        builder.Entity<Contract>().HasIndex(c => c.ProjectTitleId);
        builder.Entity<FxRate>().HasKey(r => r.Currency);
        builder.Entity<ContractNumberSequence>().HasKey(s => s.Year);
        builder.Entity<DoaPolicy>().Property(p => p.Id).ValueGeneratedNever();

        builder.Entity<Contract>()
            .HasOne(c => c.ParentContract)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Contract>()
            .HasOne(c => c.Initiator)
            .WithMany()
            .HasForeignKey(c => c.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Contract>()
            .HasOne(c => c.AssignedLawyer)
            .WithMany()
            .HasForeignKey(c => c.AssignedLawyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Contract>().HasOne(c => c.AgreementType).WithMany().HasForeignKey(c => c.AgreementTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Contract>().HasOne(c => c.InsEntity).WithMany().HasForeignKey(c => c.InsEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Contract>().HasOne(c => c.ProjectTitle).WithMany().HasForeignKey(c => c.ProjectTitleId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Contract>().HasOne(c => c.NonSupplierCategory).WithMany().HasForeignKey(c => c.NonSupplierCategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DocumentVersion>().HasOne(v => v.Contract).WithMany(c => c.Versions).HasForeignKey(v => v.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DocumentVersion>().HasOne(v => v.CreatedBy).WithMany().HasForeignKey(v => v.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApprovalStep>().HasOne(s => s.Contract).WithMany(c => c.ApprovalSteps).HasForeignKey(s => s.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ApprovalStep>().HasOne(s => s.Approver).WithMany().HasForeignKey(s => s.ApproverId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Signatory>().HasOne(s => s.Contract).WithMany(c => c.Signatories).HasForeignKey(s => s.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Obligation>().HasOne(o => o.Contract).WithMany(c => c.Obligations).HasForeignKey(o => o.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Obligation>().HasOne(o => o.Owner).WithMany().HasForeignKey(o => o.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RightsWindow>().HasOne(r => r.Contract).WithMany(c => c.Rights).HasForeignKey(r => r.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CriticalDate>().HasOne(d => d.Contract).WithMany(c => c.CriticalDates).HasForeignKey(d => d.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RelatedDocument>().HasOne(d => d.Contract).WithMany(c => c.RelatedDocuments).HasForeignKey(d => d.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RelatedDocument>().HasOne(d => d.UploadedBy).WithMany().HasForeignKey(d => d.UploadedById).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AssignmentEvent>().HasOne(a => a.Contract).WithMany(c => c.Assignments).HasForeignKey(a => a.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AssignmentEvent>().HasOne(a => a.FromLawyer).WithMany().HasForeignKey(a => a.FromLawyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AssignmentEvent>().HasOne(a => a.ToLawyer).WithMany().HasForeignKey(a => a.ToLawyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AssignmentEvent>().HasOne(a => a.AssignedBy).WithMany().HasForeignKey(a => a.AssignedById).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AuditEvent>().HasOne(a => a.Contract).WithMany(c => c.AuditEvents).HasForeignKey(a => a.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AuditEvent>().HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Notice>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Notice>().HasOne(n => n.Contract).WithMany().HasForeignKey(n => n.ContractId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void SetEnumString<TEnum>(ModelBuilder builder, Type entityType, string propertyName) where TEnum : struct, Enum
    {
        builder.Entity(entityType).Property(propertyName).HasConversion(new EnumToStringConverter<TEnum>()).HasMaxLength(48);
    }
}
