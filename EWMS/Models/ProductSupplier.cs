using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EWMS.Models;

[Table("ProductSuppliers")]
public partial class ProductSupplier
{
    [Key]
    [Column("ProductID", Order = 0)]
    public int ProductId { get; set; }

    [Key]
    [Column("SupplierID", Order = 1)]
    public int SupplierId { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductSuppliers")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SupplierId")]
    [InverseProperty("ProductSuppliers")]
    public virtual Supplier Supplier { get; set; } = null!;
}
