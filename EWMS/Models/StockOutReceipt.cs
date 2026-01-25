using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class StockOutReceipt
{
    [Key]
    [Column("StockOutID")]
    public int StockOutId { get; set; }

    [Column("WarehouseID")]
    public int WarehouseId { get; set; }

    public int IssuedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? IssuedDate { get; set; }

    [StringLength(30)]
    public string? Reason { get; set; }

    [Column("SalesOrderID")]
    public int? SalesOrderId { get; set; }

    [Column("TransferID")]
    public int? TransferId { get; set; }

    [ForeignKey("IssuedBy")]
    [InverseProperty("StockOutReceipts")]
    public virtual User IssuedByNavigation { get; set; } = null!;

    [ForeignKey("SalesOrderId")]
    [InverseProperty("StockOutReceipts")]
    public virtual SalesOrder? SalesOrder { get; set; }

    [InverseProperty("StockOut")]
    public virtual ICollection<StockOutDetail> StockOutDetails { get; set; } = new List<StockOutDetail>();

    [ForeignKey("TransferId")]
    [InverseProperty("StockOutReceipts")]
    public virtual TransferRequest? Transfer { get; set; }

    [ForeignKey("WarehouseId")]
    [InverseProperty("StockOutReceipts")]
    public virtual Warehouse Warehouse { get; set; } = null!;
}
