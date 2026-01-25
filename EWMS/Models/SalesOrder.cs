using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class SalesOrder
{
    [Key]
    [Column("SalesOrderID")]
    public int SalesOrderId { get; set; }

    [Column("WarehouseID")]
    public int WarehouseId { get; set; }

    [StringLength(150)]
    public string? CustomerName { get; set; }

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [StringLength(255)]
    public string? CustomerAddress { get; set; }

    public int CreatedBy { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TotalAmount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("SalesOrders")]
    public virtual User CreatedByNavigation { get; set; } = null!;

    [InverseProperty("SalesOrder")]
    public virtual ICollection<SalesOrderDetail> SalesOrderDetails { get; set; } = new List<SalesOrderDetail>();

    [InverseProperty("SalesOrder")]
    public virtual ICollection<StockOutReceipt> StockOutReceipts { get; set; } = new List<StockOutReceipt>();

    [ForeignKey("WarehouseId")]
    [InverseProperty("SalesOrders")]
    public virtual Warehouse Warehouse { get; set; } = null!;
}
