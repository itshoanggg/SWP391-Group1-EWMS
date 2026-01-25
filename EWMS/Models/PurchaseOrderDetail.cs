using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class PurchaseOrderDetail
{
    [Key]
    [Column("PurchaseOrderDetailID")]
    public int PurchaseOrderDetailId { get; set; }

    [Column("PurchaseOrderID")]
    public int PurchaseOrderId { get; set; }

    [Column("ProductID")]
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(29, 2)")]
    public decimal? TotalPrice { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("PurchaseOrderDetails")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("PurchaseOrderId")]
    [InverseProperty("PurchaseOrderDetails")]
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
