using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class ActivityLog
{
    [Key]
    [Column("LogID")]
    public int LogId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(100)]
    public string Action { get; set; } = null!;

    [StringLength(50)]
    public string? TableName { get; set; }

    [Column("RecordID")]
    public int? RecordId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Column("IPAddress")]
    [StringLength(50)]
    public string? Ipaddress { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("ActivityLogs")]
    public virtual User User { get; set; } = null!;
}
