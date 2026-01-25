using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[Table("Inventory")]
[Index("LocationId", Name = "IX_Inventory_LocationID")]
[Index("ProductId", Name = "IX_Inventory_ProductID")]
[Index("ProductId", "LocationId", Name = "UQ_Product_Location", IsUnique = true)]
public partial class Inventory
{
    [Key]
    [Column("InventoryID")]
    public int InventoryId { get; set; }

    [Column("ProductID")]
    public int ProductId { get; set; }

    [Column("LocationID")]
    public int LocationId { get; set; }

    public int? Quantity { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastUpdated { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("Inventories")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("Inventories")]
    public virtual Product Product { get; set; } = null!;
}
