using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[Index("WarehouseId", "LocationCode", Name = "UQ_Location_Code", IsUnique = true)]
public partial class Location
{
    [Key]
    [Column("LocationID")]
    public int LocationId { get; set; }

    [Column("WarehouseID")]
    public int WarehouseId { get; set; }

    [StringLength(50)]
    public string LocationCode { get; set; } = null!;

    [StringLength(100)]
    public string? LocationName { get; set; }

    [StringLength(20)]
    public string? Rack { get; set; }

    [InverseProperty("Location")]
    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    [InverseProperty("Location")]
    public virtual ICollection<StockInDetail> StockInDetails { get; set; } = new List<StockInDetail>();

    [InverseProperty("Location")]
    public virtual ICollection<StockOutDetail> StockOutDetails { get; set; } = new List<StockOutDetail>();

    [ForeignKey("WarehouseId")]
    [InverseProperty("Locations")]
    public virtual Warehouse Warehouse { get; set; } = null!;
}
