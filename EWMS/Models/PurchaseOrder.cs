using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class PurchaseOrder
{
    [Key]
    [Column("PurchaseOrderID")]
    public int PurchaseOrderId { get; set; }

    [Column("SupplierID")]
    public int SupplierId { get; set; }

    public int CreatedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? OrderDate { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("PurchaseOrders")]
    public virtual User CreatedByNavigation { get; set; } = null!;

    [InverseProperty("PurchaseOrder")]
    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    [InverseProperty("PurchaseOrder")]
    public virtual ICollection<StockInReceipt> StockInReceipts { get; set; } = new List<StockInReceipt>();

    [ForeignKey("SupplierId")]
    [InverseProperty("PurchaseOrders")]
    public virtual Supplier Supplier { get; set; } = null!;
}
