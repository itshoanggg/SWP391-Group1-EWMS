using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class TransferRequest
{
    [Key]
    [Column("TransferID")]
    public int TransferId { get; set; }

    [Column("FromWarehouseID")]
    public int FromWarehouseId { get; set; }

    [Column("ToWarehouseID")]
    public int ToWarehouseId { get; set; }

    [StringLength(30)]
    public string TransferType { get; set; } = null!;

    public int RequestedBy { get; set; }

    public int? ApprovedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RequestedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ApprovedDate { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [ForeignKey("ApprovedBy")]
    [InverseProperty("TransferRequestApprovedByNavigations")]
    public virtual User? ApprovedByNavigation { get; set; }

    [ForeignKey("FromWarehouseId")]
    [InverseProperty("TransferRequestFromWarehouses")]
    public virtual Warehouse FromWarehouse { get; set; } = null!;

    [ForeignKey("RequestedBy")]
    [InverseProperty("TransferRequestRequestedByNavigations")]
    public virtual User RequestedByNavigation { get; set; } = null!;

    [InverseProperty("Transfer")]
    public virtual ICollection<StockInReceipt> StockInReceipts { get; set; } = new List<StockInReceipt>();

    [InverseProperty("Transfer")]
    public virtual ICollection<StockOutReceipt> StockOutReceipts { get; set; } = new List<StockOutReceipt>();

    [ForeignKey("ToWarehouseId")]
    [InverseProperty("TransferRequestToWarehouses")]
    public virtual Warehouse ToWarehouse { get; set; } = null!;

    [InverseProperty("Transfer")]
    public virtual ICollection<TransferDetail> TransferDetails { get; set; } = new List<TransferDetail>();
}
