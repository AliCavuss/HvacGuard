using HvacGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HvacGuard.Infrastructure.Data.Configurations;

internal sealed class VesselConfiguration : IEntityTypeConfiguration<Vessel>
{
    public void Configure(EntityTypeBuilder<Vessel> b)
    {
        b.Property(v => v.Name).HasMaxLength(100).IsRequired();
        b.Property(v => v.ImoNumber).HasMaxLength(7).IsFixedLength().IsRequired();
        b.Property(v => v.VesselType).HasMaxLength(60).IsRequired();
        b.Property(v => v.Flag).HasMaxLength(60).IsRequired();
        b.Property(v => v.Owner).HasMaxLength(100);
        b.HasIndex(v => v.ImoNumber).IsUnique();
    }
}

internal sealed class AirHandlingUnitConfiguration : IEntityTypeConfiguration<AirHandlingUnit>
{
    public void Configure(EntityTypeBuilder<AirHandlingUnit> b)
    {
        b.ToTable("AirHandlingUnits");
        b.Property(u => u.Tag).HasMaxLength(20).IsRequired();
        b.Property(u => u.Name).HasMaxLength(100).IsRequired();
        b.Property(u => u.Location).HasMaxLength(100).IsRequired();
        b.Property(u => u.Manufacturer).HasMaxLength(100);
        b.Ignore(u => u.DisplayName);

        b.HasIndex(u => new { u.VesselId, u.Tag }).IsUnique();

        b.HasOne(u => u.Vessel)
            .WithMany(v => v.Units)
            .HasForeignKey(u => u.VesselId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> b)
    {
        // Time-series access pattern: "latest N readings of unit X"
        b.HasIndex(r => new { r.UnitId, r.Timestamp });

        b.HasOne(r => r.Unit)
            .WithMany(u => u.Readings)
            .HasForeignKey(r => r.UnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AlarmRuleConfiguration : IEntityTypeConfiguration<AlarmRule>
{
    public void Configure(EntityTypeBuilder<AlarmRule> b)
    {
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.Property(r => r.Parameter).HasConversion<string>().HasMaxLength(40);
        b.Property(r => r.Condition).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.Severity).HasConversion<string>().HasMaxLength(10);
    }
}

internal sealed class AlarmConfiguration : IEntityTypeConfiguration<Alarm>
{
    public void Configure(EntityTypeBuilder<Alarm> b)
    {
        b.Property(a => a.Message).HasMaxLength(300).IsRequired();
        b.Property(a => a.AcknowledgedBy).HasMaxLength(256);
        b.Property(a => a.Parameter).HasConversion<string>().HasMaxLength(40);

        b.Ignore(a => a.IsActive);
        b.Ignore(a => a.IsAcknowledged);
        b.Ignore(a => a.IsOpen);
        b.Ignore(a => a.State);

        b.HasIndex(a => new { a.UnitId, a.ClearedAt });
        b.HasIndex(a => a.RaisedAt);

        b.HasOne(a => a.Unit)
            .WithMany(u => u.Alarms)
            .HasForeignKey(a => a.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Rule)
            .WithMany()
            .HasForeignKey(a => a.AlarmRuleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> b)
    {
        b.Property(w => w.Title).HasMaxLength(150).IsRequired();
        b.Property(w => w.Description).HasMaxLength(2000);
        b.Property(w => w.ResolutionNotes).HasMaxLength(2000);
        b.Property(w => w.AssignedTo).HasMaxLength(256);
        b.Property(w => w.CreatedBy).HasMaxLength(256).IsRequired();
        b.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
        b.Ignore(w => w.Number);
        b.Ignore(w => w.IsClosed);

        b.HasIndex(w => w.Status);

        b.HasOne(w => w.Unit)
            .WithMany(u => u.WorkOrders)
            .HasForeignKey(w => w.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction avoids multiple cascade paths (Unit → Alarm → WorkOrder and Unit → WorkOrder) on SQL Server.
        b.HasOne(w => w.Alarm)
            .WithMany()
            .HasForeignKey(w => w.AlarmId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
