using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

internal static class WarehouseCompletionConfiguration
{
    public static void ConfigureWarehouseCompletion(this ModelBuilder model)
    {
        model.HasSequence<long>("warehouse_rma_number_seq");
        model.HasSequence<long>("warehouse_wave_number_seq");
        model.HasSequence<long>("warehouse_cross_dock_number_seq");
        model.Entity<WarehouseWave>(e => {
            e.ToTable("warehouse_waves"); e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasDefaultValueSql("nextval('public.warehouse_wave_number_seq')");
            e.HasIndex(x => x.Number).IsUnique(); e.HasIndex(x => x.Status);
            e.HasMany(x => x.Orders).WithOne().HasForeignKey(x => x.WaveId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<WarehouseWaveOrder>(e => {
            e.HasIndex(x => x.OutboundOrderId).IsUnique();
            e.ToTable("warehouse_wave_orders"); e.HasKey(x => new { x.WaveId, x.OutboundOrderId });
            e.HasOne<OutboundOrder>().WithMany().HasForeignKey(x => x.OutboundOrderId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WarehouseRma>(e => {
            e.ToTable("warehouse_rmas"); e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasDefaultValueSql("nextval('public.warehouse_rma_number_seq')");
            e.HasIndex(x => x.Number).IsUnique(); e.HasIndex(x => x.Status);
            e.Property(x => x.CreditAmount).HasPrecision(18, 2);
            e.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.RmaId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<WarehouseRmaLine>(e => {
            e.ToTable("warehouse_rma_lines"); e.HasKey(x => x.Id); e.Property(x => x.Qty).HasPrecision(18, 4);
            e.HasOne<SalesOrderLine>().WithMany().HasForeignKey(x => x.SalesOrderLineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WarehouseCrossDockLink>(e => {
            e.ToTable("warehouse_cross_dock_links"); e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasDefaultValueSql("nextval('public.warehouse_cross_dock_number_seq')");
            e.HasIndex(x => x.Number).IsUnique(); e.HasIndex(x => new { x.InboundShipmentLineId, x.OutboundOrderId }).IsUnique();
            e.Property(x => x.Qty).HasPrecision(18, 4);
            e.HasOne<InboundShipmentLine>().WithMany().HasForeignKey(x => x.InboundShipmentLineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OutboundOrder>().WithMany().HasForeignKey(x => x.OutboundOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WarehouseQcTemplate>(e => {
            e.ToTable("warehouse_qc_templates"); e.HasKey(x => x.Id); e.HasIndex(x => x.Name);
            e.Property(x => x.ItemsJson).HasColumnType("jsonb");
            e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WarehouseQcDefect>(e => {
            e.ToTable("warehouse_qc_defects"); e.HasKey(x => x.Id); e.HasIndex(x => x.CreatedAt); e.HasIndex(x => x.DefectType);
            e.Property(x => x.AttachmentsJson).HasColumnType("jsonb");
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SerialStatusHistory>(e => {
            e.ToTable("serial_status_history"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.SerialNumberId, x.ChangedAt });
            e.HasOne(x => x.SerialNumber).WithMany().HasForeignKey(x => x.SerialNumberId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LabelPrintJob>(e => {
            e.ToTable("label_print_jobs"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.Property(x => x.DataJson).HasColumnType("jsonb");
        });
        model.Entity<AgnumReconciliationReportEntry>(e => {
            e.ToTable("agnum_reconciliation_reports"); e.HasKey(x => x.Id); e.HasIndex(x => x.GeneratedAt);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
    }
}
