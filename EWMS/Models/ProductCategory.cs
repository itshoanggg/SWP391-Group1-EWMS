using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EWMS.Models;

public partial class ProductCategory
{
    [Key]
    [Column("CategoryID")]
    public int CategoryId { get; set; }

    [StringLength(100)]
    public string CategoryName { get; set; } = null!;

    // New optional foreign key to Supplier (column added in DB as "SuplierID")
    [Column("SupplierID")]
    public int? SupplierId { get; set; }

    [ForeignKey("SupplierId")]
    [InverseProperty("ProductCategories")]
    public virtual Supplier? Supplier { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
