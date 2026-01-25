using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class TransferDetail
{
    [Key]
    [Column("TransferDetailID")]
    public int TransferDetailId { get; set; }

    [Column("TransferID")]
    public int TransferId { get; set; }

    [Column("ProductID")]
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("TransferDetails")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("TransferId")]
    [InverseProperty("TransferDetails")]
    public virtual TransferRequest Transfer { get; set; } = null!;
}
