using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class StockOutDetail
{
    // Composite Primary Key: (StockOutID, ProductID, LocationID)
    [Column("StockOutID")]
    public int StockOutId { get; set; }

    [Column("ProductID")]
    public int ProductId { get; set; }

    [Column("LocationID")]
    public int LocationId { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
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
