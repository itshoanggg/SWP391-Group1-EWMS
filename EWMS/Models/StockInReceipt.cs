using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[Index("WarehouseId", "ReceivedDate", Name = "IX_StockInReceipts_WarehouseID_Date")]
public partial class StockInReceipt
{
    [Key]
    [Column("StockInID")]
    public int StockInId { get; set; }

    [Column("WarehouseID")]
    public int WarehouseId { get; set; }

    public int ReceivedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ReceivedDate { get; set; }

    [StringLength(30)]
    public string? Reason { get; set; }

    [Column("PurchaseOrderID")]
    public int? PurchaseOrderId { get; set; }

    [Column("TransferID")]
    public int? TransferId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TotalAmount { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("PurchaseOrderId")]
    [InverseProperty("StockInReceipts")]
    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey("ReceivedBy")]
    [InverseProperty("StockInReceipts")]
    public virtual User ReceivedByNavigation { get; set; } = null!;

    [InverseProperty("StockIn")]
    public virtual ICollection<StockInDetail> StockInDetails { get; set; } = new List<StockInDetail>();

    [ForeignKey("TransferId")]
    [InverseProperty("StockInReceipts")]
    public virtual TransferRequest? Transfer { get; set; }

    [ForeignKey("WarehouseId")]
    [InverseProperty("StockInReceipts")]
    public virtual Warehouse Warehouse { get; set; } = null!;
}
