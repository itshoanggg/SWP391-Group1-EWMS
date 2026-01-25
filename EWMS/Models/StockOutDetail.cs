using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[Index("StockOutId", Name = "IX_StockOutDetails_StockOutID")]
public partial class StockOutDetail
{
    [Key]
    [Column("StockOutDetailID")]
    public int StockOutDetailId { get; set; }

    [Column("StockOutID")]
    public int StockOutId { get; set; }

    [Column("ProductID")]
    public int ProductId { get; set; }

    [Column("LocationID")]
    public int LocationId { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(29, 2)")]
    public decimal? TotalPrice { get; set; }

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
