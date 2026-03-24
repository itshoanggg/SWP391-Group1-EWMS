using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class Product
{
    [Key]
    [Column("ProductID")]
    public int ProductId { get; set; }

    [StringLength(150)]
    public string ProductName { get; set; } = null!;

    [Column("CategoryID")]
    public int? CategoryId { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? CostPrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SellingPrice { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("Products")]
    public virtual ProductCategory? Category { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    [InverseProperty("Product")]
    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    [InverseProperty("Product")]
    public virtual ICollection<SalesOrderDetail> SalesOrderDetails { get; set; } = new List<SalesOrderDetail>();

    [InverseProperty("Product")]
    public virtual ICollection<StockInDetail> StockInDetails { get; set; } = new List<StockInDetail>();

    [InverseProperty("Product")]
    public virtual ICollection<StockOutDetail> StockOutDetails { get; set; } = new List<StockOutDetail>();

    [InverseProperty("Product")]
    public virtual ICollection<TransferDetail> TransferDetails { get; set; } = new List<TransferDetail>();
}
