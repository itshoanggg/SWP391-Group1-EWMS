using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[PrimaryKey("StockInId", "ProductId", "LocationId")]
public partial class StockInDetail
{
    [Key]
    [Column("StockInID")]
    public int StockInId { get; set; }

    [Key]
    [Column("ProductID")]
    public int ProductId { get; set; }

    [Key]
    [Column("LocationID")]
    public int LocationId { get; set; }

    public int Quantity { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("StockInDetails")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("StockInDetails")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("StockInId")]
    [InverseProperty("StockInDetails")]
    public virtual StockInReceipt StockIn { get; set; } = null!;
}
