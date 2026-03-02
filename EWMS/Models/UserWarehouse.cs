using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class UserWarehouse
{
    // Composite Primary Key: (UserID, WarehouseID)
    [Column("UserID")]
    public int UserId { get; set; }

    [Column("WarehouseID")]
    public int WarehouseId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AssignedDate { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserWarehouses")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("WarehouseId")]
    [InverseProperty("UserWarehouses")]
    public virtual Warehouse Warehouse { get; set; } = null!;
}
