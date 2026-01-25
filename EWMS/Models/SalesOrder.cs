using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class SalesOrder
{
    [Key]
    [Column("SalesOrderID")]
    public int SalesOrderId { get; set; }

    public int CreatedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? OrderDate { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("SalesOrders")]
    public virtual User CreatedByNavigation { get; set; } = null!;

    [InverseProperty("SalesOrder")]
    public virtual ICollection<SalesOrderDetail> SalesOrderDetails { get; set; } = new List<SalesOrderDetail>();

    [InverseProperty("SalesOrder")]
    public virtual ICollection<StockOutReceipt> StockOutReceipts { get; set; } = new List<StockOutReceipt>();
}
