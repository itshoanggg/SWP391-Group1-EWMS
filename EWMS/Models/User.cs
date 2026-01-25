using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

[Index("Username", Name = "UQ__Users__536C85E43E7BDF72", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(50)]
    public string Username { get; set; } = null!;

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(100)]
    public string? FullName { get; set; }

    [Column("RoleID")]
    public int RoleId { get; set; }

    public bool? IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role Role { get; set; } = null!;

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();

    [InverseProperty("ReceivedByNavigation")]
    public virtual ICollection<StockInReceipt> StockInReceipts { get; set; } = new List<StockInReceipt>();

    [InverseProperty("IssuedByNavigation")]
    public virtual ICollection<StockOutReceipt> StockOutReceipts { get; set; } = new List<StockOutReceipt>();

    [InverseProperty("ApprovedByNavigation")]
    public virtual ICollection<TransferRequest> TransferRequestApprovedByNavigations { get; set; } = new List<TransferRequest>();

    [InverseProperty("RequestedByNavigation")]
    public virtual ICollection<TransferRequest> TransferRequestRequestedByNavigations { get; set; } = new List<TransferRequest>();

    [InverseProperty("User")]
    public virtual ICollection<UserWarehouse> UserWarehouses { get; set; } = new List<UserWarehouse>();
}
