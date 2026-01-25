using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[PrimaryKey("ProductId", "LocationId")]
[Table("Inventory")]
public partial class Inventory
{
    [Key]
    [Column("ProductID")]
    public int ProductId { get; set; }

    [Key]
    [Column("LocationID")]
    public int LocationId { get; set; }

    public int? Quantity { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("Inventories")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("Inventories")]
    public virtual Product Product { get; set; } = null!;
}
