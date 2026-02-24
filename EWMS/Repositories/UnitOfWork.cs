using EWMS.Models;
using EWMS.Repositories.Interfaces;

namespace EWMS.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EWMSDbContext _context;

        public EWMS.Repositories.Interfaces.IPurchaseOrderRepository PurchaseOrders { get; private set; }
        public EWMS.Repositories.Interfaces.IStockInRepository StockIns { get; private set; }
        public EWMS.Repositories.Interfaces.IInventoryRepository Inventories { get; private set; }
        public EWMS.Repositories.Interfaces.IProductRepository Products { get; private set; }
        public EWMS.Repositories.Interfaces.ILocationRepository Locations { get; private set; }
        public EWMS.Repositories.Interfaces.IWarehouseRepository Warehouses { get; private set; }
        public EWMS.Repositories.Interfaces.IUserWarehouseRepository UserWarehouses { get; private set; }
        public EWMS.Repositories.Interfaces.ISupplierRepository Suppliers { get; private set; }

        public UnitOfWork(EWMSDbContext context)
        {
            _context = context;

            PurchaseOrders = new PurchaseOrderRepository(_context);
            StockIns = new StockInRepository(_context);
            Inventories = new InventoryRepository(_context);
            Products = new ProductRepository(_context);
            Locations = new LocationRepository(_context);
            Warehouses = new WarehouseRepository(_context);
            UserWarehouses = new UserWarehouseRepository(_context);
            Suppliers = new SupplierRepository(_context);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
