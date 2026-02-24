# 📚 GIẢI THÍCH CHI TIẾT UNITOFWORK PATTERN

---

## 🎯 1. UNITOFWORK LÀ GÌ?

**UnitOfWork** là một design pattern giúp:
- Quản lý **nhiều repositories** trong **một transaction**
- Đảm bảo tất cả thay đổi được **commit cùng lúc** hoặc **rollback hết**
- Tránh **duplicate DbContext** instances

---

## 🔴 2. VÍ DỤ KHÔNG CÓ UNITOFWORK

### ❌ Cách 1: Inject từng Repository riêng lẻ

```csharp
// Program.cs - Dependency Injection
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IStockInRepository, StockInRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
// ... 10+ repositories khác

// StockInService.cs - Constructor hell!
public class StockInService : IStockInService
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepo;
    private readonly IStockInRepository _stockInRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IProductRepository _productRepo;
    private readonly ILocationRepository _locationRepo;
    private readonly EWMSContext _context; // Cần context để SaveChanges!
    
    public StockInService(
        IPurchaseOrderRepository purchaseOrderRepo,
        IStockInRepository stockInRepo,
        IInventoryRepository inventoryRepo,
        IProductRepository productRepo,
        ILocationRepository locationRepo,
        EWMSContext context) // 6 DEPENDENCIES!
    {
        _purchaseOrderRepo = purchaseOrderRepo;
        _stockInRepo = stockInRepo;
        _inventoryRepo = inventoryRepo;
        _productRepo = productRepo;
        _locationRepo = locationRepo;
        _context = context;
    }
    
    public async Task ConfirmStockInAsync(ConfirmStockInRequest request, int userId)
    {
        // Vấn đề: Mỗi repository có thể dùng KHÁC DbContext instance!
        var po = await _purchaseOrderRepo.GetByIdAsync(request.PurchaseOrderId);
        
        var stockIn = new StockInReceipt { ... };
        await _stockInRepo.AddAsync(stockIn);
        
        foreach (var detail in request.Details)
        {
            var inventory = await _inventoryRepo.GetByLocationAndProductAsync(...);
            inventory.Quantity += detail.Quantity;
            await _inventoryRepo.UpdateAsync(inventory);
        }
        
        po.Status = "Received";
        await _purchaseOrderRepo.UpdateAsync(po);
        
        // ⚠️ NGUY HIỂM: Phải gọi SaveChanges trên context
        // Nhưng repositories có thể dùng context khác!
        await _context.SaveChangesAsync();
    }
}
```

**❌ Vấn đề:**
1. **Constructor quá dài** - 6+ dependencies
2. **Khó maintain** - thêm repository mới = sửa constructor
3. **Khó test** - phải mock 6+ dependencies
4. **Không đảm bảo transaction** - repositories có thể dùng khác DbContext
5. **Code duplicate** - mọi Service đều phải inject giống nhau

---

### ❌ Cách 2: Inject DbContext trực tiếp (No Repository Pattern)

```csharp
// StockInService.cs
public class StockInService : IStockInService
{
    private readonly EWMSContext _context;
    
    public StockInService(EWMSContext context)
    {
        _context = context;
    }
    
    public async Task ConfirmStockInAsync(ConfirmStockInRequest request, int userId)
    {
        // Truy cập DbSet trực tiếp - mất abstraction layer
        var po = await _context.PurchaseOrders
            .Include(p => p.PurchaseOrderDetails)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == request.PurchaseOrderId);
        
        var stockIn = new StockInReceipt
        {
            PurchaseOrderId = request.PurchaseOrderId,
            ReceivedDate = DateTime.Now,
            ReceivedBy = userId
        };
        _context.StockInReceipts.Add(stockIn);
        
        foreach (var detail in request.Details)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.LocationId == detail.LocationId 
                                       && i.ProductId == detail.ProductId);
            
            if (inventory == null)
            {
                inventory = new Inventory
                {
                    LocationId = detail.LocationId,
                    ProductId = detail.ProductId,
                    Quantity = detail.Quantity
                };
                _context.Inventories.Add(inventory);
            }
            else
            {
                inventory.Quantity += detail.Quantity;
            }
        }
        
        po.Status = "Received";
        
        await _context.SaveChangesAsync();
    }
}
```

**❌ Vấn đề:**
1. **Service biết quá nhiều về database** - vi phạm separation of concerns
2. **Khó test** - phải mock EF Core DbSet
3. **Code duplicate** - LINQ queries lặp lại nhiều nơi
4. **Tight coupling** - Service phụ thuộc trực tiếp vào EF Core
5. **Khó maintain** - thay đổi database = sửa nhiều Services

---

## ✅ 3. VÍ DỤ CÓ UNITOFWORK

### ✅ Cách 3: UnitOfWork Pattern (BEST PRACTICE)

```csharp
// IUnitOfWork.cs - Interface
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
    
    Task<int> SaveChangesAsync(); // ← Centralized transaction control
}

// UnitOfWork.cs - Implementation
public class UnitOfWork : IUnitOfWork
{
    private readonly EWMSContext _context; // ← Single DbContext instance
    
    public IPurchaseOrderRepository PurchaseOrders { get; private set; }
    public IStockInRepository StockIns { get; private set; }
    public IInventoryRepository Inventories { get; private set; }
    // ... other repositories
    
    public UnitOfWork(EWMSContext context)
    {
        _context = context;
        
        // ✅ TẤT CẢ repositories dùng CHUNG 1 DbContext instance
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
        // ✅ Tất cả thay đổi từ MỌI repositories được commit cùng lúc
        return await _context.SaveChangesAsync();
    }
    
    public void Dispose()
    {
        _context.Dispose();
    }
}

// Program.cs - Dependency Injection
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); // ← Chỉ 1 dòng!

// StockInService.cs - Clean & Simple!
public class StockInService : IStockInService
{
    private readonly IUnitOfWork _unitOfWork; // ← Only 1 dependency!
    
    public StockInService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task<StockInReceipt> ConfirmStockInAsync(
        ConfirmStockInRequest request, 
        int userId)
    {
        // ✅ Truy cập repositories qua UnitOfWork
        var po = await _unitOfWork.PurchaseOrders
            .GetByIdWithDetailsAsync(request.PurchaseOrderId);
        
        if (po == null)
            throw new Exception("Không tìm thấy đơn hàng");
        
        // Tạo Stock-In Receipt
        var stockIn = new StockInReceipt
        {
            PurchaseOrderId = request.PurchaseOrderId,
            WarehouseId = request.WarehouseId,
            ReceivedDate = DateTime.Now,
            ReceivedBy = userId,
            Notes = request.Notes
        };
        
        await _unitOfWork.StockIns.AddAsync(stockIn);
        
        // Xử lý từng detail
        foreach (var detail in request.Details)
        {
            // Thêm stock-in detail
            var stockInDetail = new StockInDetail
            {
                StockInId = stockIn.StockInId,
                ProductId = detail.ProductId,
                LocationId = detail.LocationId,
                Quantity = detail.Quantity
            };
            
            await _unitOfWork.StockIns.AddDetailAsync(stockInDetail);
            
            // Cập nhật inventory
            var inventory = await _unitOfWork.Inventories
                .GetByLocationAndProductAsync(detail.LocationId, detail.ProductId);
            
            if (inventory == null)
            {
                inventory = new Inventory
                {
                    LocationId = detail.LocationId,
                    ProductId = detail.ProductId,
                    Quantity = detail.Quantity,
                    LastUpdated = DateTime.Now
                };
                await _unitOfWork.Inventories.AddAsync(inventory);
            }
            else
            {
                inventory.Quantity += detail.Quantity;
                inventory.LastUpdated = DateTime.Now;
                await _unitOfWork.Inventories.UpdateAsync(inventory);
            }
        }
        
        // Cập nhật PO status
        po.Status = "Received";
        po.ReceivedDate = DateTime.Now;
        await _unitOfWork.PurchaseOrders.UpdateAsync(po);
        
        // ✅ COMMIT TẤT CẢ thay đổi trong 1 transaction
        // Nếu có lỗi → TẤT CẢ đều rollback
        await _unitOfWork.SaveChangesAsync();
        
        return stockIn;
    }
}
```

---

## 🔥 4. SO SÁNH TRỰC TIẾP

### Scenario: Confirm Stock-In với 10 products

| Tiêu chí | Không UnitOfWork | Có UnitOfWork |
|----------|------------------|---------------|
| **Dependencies** | 6+ repositories + DbContext | 1 UnitOfWork |
| **Constructor** | 7 parameters | 1 parameter |
| **Testing** | Mock 7 dependencies | Mock 1 dependency |
| **Transaction Safety** | ❌ Không đảm bảo | ✅ Đảm bảo 100% |
| **Code Duplication** | ❌ Cao | ✅ Thấp |
| **Maintainability** | ❌ Khó | ✅ Dễ |
| **DbContext Instances** | ⚠️ Có thể khác nhau | ✅ Chỉ 1 instance |
| **Separation of Concerns** | ❌ Vi phạm | ✅ Tuân thủ |

---

## ⚠️ 5. VÍ DỤ KHI KHÔNG CÓ TRANSACTION

### Không có UnitOfWork:

```csharp
// Giả sử mỗi repository dùng khác DbContext instance
var po = await _purchaseOrderRepo.GetByIdAsync(1);
await _purchaseOrderRepo.UpdateAsync(po); // ← DbContext #1

var stockIn = new StockInReceipt { ... };
await _stockInRepo.AddAsync(stockIn); // ← DbContext #2

// ❌ DISASTER: SaveChanges ở repository #1 thành công
// Nhưng repository #2 bị lỗi → Data inconsistent!

// Inventory đã được cập nhật nhưng StockIn không có!
```

### Có UnitOfWork:

```csharp
var po = await _unitOfWork.PurchaseOrders.GetByIdAsync(1);
await _unitOfWork.PurchaseOrders.UpdateAsync(po);

var stockIn = new StockInReceipt { ... };
await _unitOfWork.StockIns.AddAsync(stockIn);

// ✅ SAFE: TẤT CẢ dùng chung 1 DbContext
// Nếu có lỗi → TẤT CẢ rollback
await _unitOfWork.SaveChangesAsync();
```

---

## 📊 6. LUỒNG DỮ LIỆU VỚI UNITOFWORK

```
Controller
    ↓ inject
Service (có IUnitOfWork)
    ↓ sử dụng
UnitOfWork (quản lý tất cả repositories)
    ↓ cung cấp
Repositories (PurchaseOrderRepo, StockInRepo, InventoryRepo...)
    ↓ dùng chung
DbContext (1 instance duy nhất)
    ↓ thao tác
Database

Khi gọi SaveChangesAsync():
    ← SaveChanges
DbContext (commit/rollback TẤT CẢ)
    ← return result
UnitOfWork
    ← return result
Service
    ← return result
Controller
```

---

## 💡 7. KẾT LUẬN

**UnitOfWork Pattern là ESSENTIAL cho:**
- ✅ **Transaction Management** - Đảm bảo data consistency
- ✅ **Clean Architecture** - Separation of concerns
- ✅ **Testability** - Dễ mock, dễ test
- ✅ **Maintainability** - Dễ thêm/sửa/xóa repositories
- ✅ **Performance** - 1 DbContext instance, tracking hiệu quả

**Không nên xóa trừ khi:**
- ❌ App rất đơn giản (1-2 tables)
- ❌ Không cần transactions
- ❌ Chỉ làm CRUD cơ bản

**Trong trường hợp EWMS:**
- ✅ Nhiều tables liên quan (PO, StockIn, Inventory, Location...)
- ✅ Cần transactions phức tạp
- ✅ Enterprise-level app
→ **PHẢI GIỮ UnitOfWork!**

---

## 🎯 8. VÍ DỤ THỰC TẾ TRONG EWMS

### Scenario: Nhập kho 100 sản phẩm vào 5 racks khác nhau

**Không có UnitOfWork:**
```
1. Insert StockInReceipt ✅
2. Insert StockInDetail #1 ✅
3. Update Inventory rack A01 ✅
4. Insert StockInDetail #2 ✅
5. Update Inventory rack A02 ✅
6. Insert StockInDetail #3 ❌ LỖI!
7. ← Rollback? KHÔNG! Đã commit 1,2,3,4,5
8. → Database bị sai: có StockIn nhưng Inventory thiếu
```

**Có UnitOfWork:**
```
1. Insert StockInReceipt (tracked)
2. Insert StockInDetail #1 (tracked)
3. Update Inventory rack A01 (tracked)
4. Insert StockInDetail #2 (tracked)
5. Update Inventory rack A02 (tracked)
6. Insert StockInDetail #3 (tracked) ❌ LỖI!
7. ← SaveChangesAsync() failed
8. → TẤT CẢ rollback tự động
9. → Database vẫn đúng!
```

---

Hy vọng bạn hiểu rõ tầm quan trọng của UnitOfWork Pattern! 🚀
