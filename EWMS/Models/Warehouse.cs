using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class Warehouse
{
    [Key]
    [Column("WarehouseID")]
    public int WarehouseId { get; set; }

    [StringLength(100)]
    public string WarehouseName { get; set; } = null!;

    [InverseProperty("Warehouse")]
    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();

    [InverseProperty("Warehouse")]
    public virtual ICollection<StockInReceipt> StockInReceipts { get; set; } = new List<StockInReceipt>();

    [InverseProperty("Warehouse")]
    public virtual ICollection<StockOutReceipt> StockOutReceipts { get; set; } = new List<StockOutReceipt>();

    [InverseProperty("FromWarehouse")]
    public virtual ICollection<TransferRequest> TransferRequestFromWarehouses { get; set; } = new List<TransferRequest>();

    [InverseProperty("ToWarehouse")]
    public virtual ICollection<TransferRequest> TransferRequestToWarehouses { get; set; } = new List<TransferRequest>();

    [InverseProperty("Warehouse")]
    public virtual ICollection<UserWarehouse> UserWarehouses { get; set; } = new List<UserWarehouse>();
}
