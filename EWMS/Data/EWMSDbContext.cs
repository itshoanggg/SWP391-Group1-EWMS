using EWMS.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;

namespace EWMS.Data
{
    public class EWMSDbContext : DbContext
    {
        public EWMSDbContext(DbContextOptions<EWMSDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<UserWarehouse> UserWarehouses { get; set; }
    }
}
