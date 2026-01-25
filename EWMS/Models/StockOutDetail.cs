using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[PrimaryKey("StockOutId", "ProductId", "LocationId")]
public partial class StockOutDetail
{
    [Key]
    [Column("StockOutID")]
    public int StockOutId { get; set; }

    [Key]
    [Column("ProductID")]
    public int ProductId { get; set; }

    [Key]
    [Column("LocationID")]
    public int LocationId { get; set; }

    public int Quantity { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("StockOutDetails")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("StockOutDetails")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("StockOutId")]
    [InverseProperty("StockOutDetails")]
    public virtual StockOutReceipt StockOut { get; set; } = null!;
}
