using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class EWMSContext : DbContext
{
    public EWMSContext()
    {
    }

    public EWMSContext(DbContextOptions<EWMSContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductCategory> ProductCategories { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SalesOrder> SalesOrders { get; set; }

    public virtual DbSet<SalesOrderDetail> SalesOrderDetails { get; set; }

    public virtual DbSet<StockInDetail> StockInDetails { get; set; }

    public virtual DbSet<StockInReceipt> StockInReceipts { get; set; }

    public virtual DbSet<StockOutDetail> StockOutDetails { get; set; }

    public virtual DbSet<StockOutReceipt> StockOutReceipts { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TransferDetail> TransferDetails { get; set; }

    public virtual DbSet<TransferRequest> TransferRequests { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserWarehouse> UserWarehouses { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DBContext");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.Property(e => e.Quantity).HasDefaultValue(0);

            entity.HasOne(d => d.Location).WithMany(p => p.Inventories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inventory_Locations");

            entity.HasOne(d => d.Product).WithMany(p => p.Inventories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inventory_Products");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("PK__Location__E7FEA4773AABBA9D");

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Locations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Locations_Warehouses");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6EDF19BF9E4");

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Category).WithMany(p => p.Products).HasConstraintName("FK_Products_Categories");
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__ProductC__19093A2B72635DA0");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.PurchaseOrderId).HasName("PK__Purchase__036BAC44E63D70BD");

            entity.Property(e => e.OrderDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseOrders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrders_Users");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseOrders_Suppliers");
        });

        modelBuilder.Entity<PurchaseOrderDetail>(entity =>
        {
            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseOrderDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POD_Products");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POD_PurchaseOrders");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3A952A33DE");
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.HasKey(e => e.SalesOrderId).HasName("PK__SalesOrd__B14003C2B2CCFFAB");

            entity.Property(e => e.OrderDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SalesOrders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesOrders_Users");
        });

        modelBuilder.Entity<SalesOrderDetail>(entity =>
        {
            entity.HasOne(d => d.Product).WithMany(p => p.SalesOrderDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOD_Products");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.SalesOrderDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOD_SalesOrders");
        });

        modelBuilder.Entity<StockInDetail>(entity =>
        {
            entity.HasOne(d => d.Location).WithMany(p => p.StockInDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SID_Location");

            entity.HasOne(d => d.Product).WithMany(p => p.StockInDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SID_Product");

            entity.HasOne(d => d.StockIn).WithMany(p => p.StockInDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SID_StockIn");
        });

        modelBuilder.Entity<StockInReceipt>(entity =>
        {
            entity.HasKey(e => e.StockInId).HasName("PK__StockInR__794DA64C84A1EF3E");

            entity.Property(e => e.ReceivedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.StockInReceipts).HasConstraintName("FK_StockIn_PurchaseOrder");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.StockInReceipts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockIn_ReceivedBy");

            entity.HasOne(d => d.Transfer).WithMany(p => p.StockInReceipts).HasConstraintName("FK_StockIn_Transfer");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockInReceipts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockIn_Warehouse");
        });

        modelBuilder.Entity<StockOutDetail>(entity =>
        {
            entity.HasOne(d => d.Location).WithMany(p => p.StockOutDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOD_Location");

            entity.HasOne(d => d.Product).WithMany(p => p.StockOutDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOD_Product");

            entity.HasOne(d => d.StockOut).WithMany(p => p.StockOutDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOD_StockOut");
        });

        modelBuilder.Entity<StockOutReceipt>(entity =>
        {
            entity.HasKey(e => e.StockOutId).HasName("PK__StockOut__C5308D9AF76E029D");

            entity.Property(e => e.IssuedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.StockOutReceipts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOut_IssuedBy");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.StockOutReceipts).HasConstraintName("FK_StockOut_SalesOrder");

            entity.HasOne(d => d.Transfer).WithMany(p => p.StockOutReceipts).HasConstraintName("FK_StockOut_Transfer");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockOutReceipts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOut_Warehouse");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666945AA97937");
        });

        modelBuilder.Entity<TransferDetail>(entity =>
        {
            entity.HasKey(e => e.TransferDetailId).HasName("PK__Transfer__F9BF690FEA2D281E");

            entity.HasOne(d => d.Product).WithMany(p => p.TransferDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDetails_Product");

            entity.HasOne(d => d.Transfer).WithMany(p => p.TransferDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferDetails_Transfer");
        });

        modelBuilder.Entity<TransferRequest>(entity =>
        {
            entity.HasKey(e => e.TransferId).HasName("PK__Transfer__95490171F8BC22A5");

            entity.Property(e => e.RequestedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.TransferRequestApprovedByNavigations).HasConstraintName("FK_TR_ApprovedBy");

            entity.HasOne(d => d.FromWarehouse).WithMany(p => p.TransferRequestFromWarehouses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TR_FromWarehouse");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.TransferRequestRequestedByNavigations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TR_RequestedBy");

            entity.HasOne(d => d.ToWarehouse).WithMany(p => p.TransferRequestToWarehouses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TR_ToWarehouse");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC920AA516");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<UserWarehouse>(entity =>
        {
            entity.HasKey(e => e.UserWarehouseId).HasName("PK__UserWare__BEEFA846C68072C1");

            entity.Property(e => e.AssignedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsPrimary).HasDefaultValue(true);

            entity.HasOne(d => d.User).WithMany(p => p.UserWarehouses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserWarehouses_Users");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.UserWarehouses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserWarehouses_Warehouses");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.WarehouseId).HasName("PK__Warehous__2608AFD973461DFF");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
