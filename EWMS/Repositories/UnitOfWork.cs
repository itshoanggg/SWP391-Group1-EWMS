using EWMS.Models;
using EWMS.Repositories.Interfaces;

namespace EWMS.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EWMSContext _context;

        public IPurchaseOrderRepository PurchaseOrders { get; private set; }
        public IStockInRepository StockIns { get; private set; }
        public IInventoryRepository Inventories { get; private set; }
        public IProductRepository Products { get; private set; }
        public ILocationRepository Locations { get; private set; }
        public IWarehouseRepository Warehouses { get; private set; }
        public IUserWarehouseRepository UserWarehouses { get; private set; }
        public ISupplierRepository Suppliers { get; private set; }

        public UnitOfWork(EWMSContext context)
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
