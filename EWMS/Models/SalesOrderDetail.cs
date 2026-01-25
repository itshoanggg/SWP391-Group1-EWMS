using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[PrimaryKey("SalesOrderId", "ProductId")]
public partial class SalesOrderDetail
{
    [Key]
    [Column("SalesOrderID")]
    public int SalesOrderId { get; set; }

    [Key]
    [Column("ProductID")]
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? UnitPrice { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("SalesOrderDetails")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SalesOrderId")]
    [InverseProperty("SalesOrderDetails")]
    public virtual SalesOrder SalesOrder { get; set; } = null!;
}
