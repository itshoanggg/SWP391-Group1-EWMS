# Yêu Cầu: Nhập Kho Chi Tiết (Stock In Details)

## Mô Tả Chức Năng
Từ một Purchase Order, Inventory Staff phân bổ hàng hóa vào các vị trí cụ thể trong kho và xác nhận nhập kho.

## Yêu Cầu Nghiệp Vụ

### Màn Hình Hiển Thị

**Thông tin đơn hàng (Read-only):**
- Mã PO
- Nhà cung cấp
- Ngày dự kiến nhận
- Danh sách sản phẩm đã đặt (product, quantity ordered, đã nhập, còn lại)

**Phần nhập kho:**
- Chọn sản phẩm cần nhập
- Chọn vị trí kho (location)
- Nhập số lượng nhập
- Kiểm tra dung lượng vị trí
- Thêm nhiều dòng allocation
- Hiển thị tổng đã phân bổ vs còn lại

### Quy Tắc
- Chỉ nhập được số lượng <= còn lại của PO
- Kiểm tra vị trí có đủ chỗ không (capacity)
- Một product có thể nhập vào nhiều locations
- Phải nhập ít nhất 1 allocation
- Ngày nhập kho = hôm nay

### Khi Confirm
1. Tạo StockInReceipt (phiếu nhập kho)
2. Tạo các StockInDetails (chi tiết nhập)
3. Cập nhật Inventory (tăng quantity tại locations)
4. Cập nhật PurchaseOrder status:
   - Nếu nhập đủ → Received
   - Nếu nhập 1 phần → PartiallyReceived
5. Hiển thị message thành công với mã phiếu (SI-0001)

## Yêu Cầu Kỹ Thuật

### Backend

**GET Details/{id}:**
- Load PurchaseOrder với details
- Check status phải là Ordered hoặc PartiallyReceived
- Pass warehouseId, userId vào ViewBag

**API Cần Có:**
- GET `/StockIn/GetPurchaseOrderInfo?purchaseOrderId=1`
  → Info + danh sách products với remaining quantity
  
- GET `/StockIn/GetAvailableLocations?warehouseId=1&productId=1`
  → Danh sách locations có thể nhập sản phẩm này
  
- GET `/StockIn/CheckLocationCapacity?locationId=1`
  → Current usage và capacity limit

- POST `/StockIn/ConfirmStockIn`
  → Body: { PurchaseOrderId, WarehouseId, Allocations: [{ProductId, LocationId, Quantity}] }
  → Create receipt, update inventory, update PO status

### Frontend

**JavaScript:**
- Dynamic table để add/remove allocations
- Auto-load locations khi chọn product
- Validate quantity <= remaining
- Check capacity khi chọn location
- Calculate totals
- AJAX submit

**UI:**
- Phần trên: PO info (read-only table)
- Phần dưới: Allocation form (dynamic)
- Summary: Tổng đã phân bổ vs còn phải nhập
- Confirm button (disabled nếu invalid)

## Data Models

**StockInReceipt:**
- StockInId
- PurchaseOrderId
- WarehouseId
- ReceivedDate
- ReceivedBy (userId)
- Status

**StockInDetail:**
- StockInDetailId
- StockInId
- ProductId
- LocationId
- Quantity
- UnitPrice
- TotalPrice

**Inventory:**
- LocationId
- ProductId
- Quantity (cập nhật += quantity nhập)

## Business Logic

### Validate Allocation
```
For each allocation:
- Check product thuộc PO không
- Check quantity > 0
- Check total allocated <= remaining của product trong PO
- Check location capacity
```

### Update Inventory
```
For each allocation:
  Find or Create Inventory(LocationId, ProductId)
  Inventory.Quantity += Allocation.Quantity
```

### Update PO Status
```
TotalOrdered = Sum(PO.Details.Quantity)
TotalReceived = Sum(StockInDetails where PO = x)

If TotalReceived >= TotalOrdered:
  Status = "Received"
Else:
  Status = "PartiallyReceived"
```

## Gợi Ý Implement

### ViewModel
```
ConfirmStockInRequest {
    PurchaseOrderId: int
    WarehouseId: int
    ReceivedDate: DateTime
    Allocations: List<AllocationDTO>
}

AllocationDTO {
    ProductId: int
    LocationId: int
    Quantity: int
}
```

### Service Method
```
ConfirmStockInAsync(request, userId)
{
    1. Validate request
    2. Begin transaction
    3. Create StockInReceipt
    4. Create StockInDetails
    5. Update Inventory for each allocation
    6. Update PurchaseOrder status
    7. Commit transaction
    8. Return receipt
}
```

### Location Capacity Check
```
Available = Location.Capacity - CurrentUsage
Where CurrentUsage = Sum(Inventory.Quantity at this location)
```

## Lưu Ý
- Sử dụng database transaction (rollback nếu có lỗi)
- Log activity
- Handle concurrent updates
- Real-time capacity check
