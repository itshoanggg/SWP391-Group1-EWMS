using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[Index("UserId", Name = "IX_UserWarehouses_UserID")]
[Index("WarehouseId", Name = "IX_UserWarehouses_WarehouseID")]
[Index("UserId", "WarehouseId", Name = "UQ_User_Warehouse", IsUnique = true)]
public partial class UserWarehouse
{
    [Key]
    [Column("UserWarehouseID")]
    public int UserWarehouseId { get; set; }

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
