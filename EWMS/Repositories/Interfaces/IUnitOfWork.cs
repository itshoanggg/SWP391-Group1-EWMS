namespace EWMS.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IPurchaseOrderRepository PurchaseOrders { get; }
        IStockInRepository StockIns { get; }
        IInventoryRepository Inventories { get; }
        IProductRepository Products { get; }
        ILocationRepository Locations { get; }
        IWarehouseRepository Warehouses { get; }
        IUserWarehouseRepository UserWarehouses { get; }
        ISupplierRepository Suppliers { get; }

        Task<int> SaveChangesAsync();
    }
}
