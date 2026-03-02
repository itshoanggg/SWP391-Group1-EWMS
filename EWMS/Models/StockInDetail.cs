using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class StockInDetail
{
    // Composite Primary Key: (StockInID, ProductID, LocationID)
    [Column("StockInID")]
    public int StockInId { get; set; }

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
    [InverseProperty("StockInDetails")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("StockInDetails")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("StockInId")]
    [InverseProperty("StockInDetails")]
    public virtual StockInReceipt StockIn { get; set; } = null!;
}
