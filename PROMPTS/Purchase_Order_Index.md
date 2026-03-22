# Yêu Cầu: Trang Danh Sách Đơn Mua Hàng (Purchase Order Index)

## Mô Tả Chức Năng
Tôi cần tạo trang danh sách đơn mua hàng cho nhân viên mua hàng (Purchasing Staff) xem và quản lý các đơn hàng của kho mình phụ trách.

## Yêu Cầu Nghiệp Vụ

### Ai được sử dụng?
- Chỉ Purchasing Staff
- Chỉ xem được đơn hàng của kho mình được phân công

### Hiển thị gì?
Danh sách đơn mua hàng với thông tin:
- Mã đơn hàng (format: PO-0001, PO-0002...)
- Nhà cung cấp
- Ngày tạo đơn
- Ngày dự kiến nhận hàng
- Tổng số lượng sản phẩm
- Tổng giá trị đơn hàng
- Trạng thái (Ordered, Delivered, Received, Cancelled)
- Người tạo đơn

### Tương Tác
- Filter theo trạng thái (mặc định hiển thị "Ordered")
- Tìm kiếm theo mã đơn hoặc tên nhà cung cấp
- Nút tạo đơn mới
- Xem chi tiết đơn hàng
- Đánh dấu "Hàng đã về kho" (chỉ cho đơn Ordered)
- Hủy đơn hàng (chỉ cho đơn Ordered)

## Yêu Cầu Kỹ Thuật

### Backend (ASP.NET Core MVC)
- Controller: PurchaseOrderController
- Action: Index
- Pattern: Repository + Service layer
- Authorize: Role = "Purchasing Staff"

**Luồng xử lý:**
1. Kiểm tra user đã login chưa
2. Lấy warehouse mà user được assign
3. Gọi service lấy danh sách purchase orders theo warehouse và status
4. Trả về View với danh sách

**API cần có:**
- GET `/PurchaseOrder/GetPurchaseOrderList?status=Ordered&search=` (AJAX)
- POST `/PurchaseOrder/MarkAsDelivered` (AJAX)
- POST `/PurchaseOrder/Cancel` (AJAX)

### Frontend
- View Razor với table responsive
- Tabs để chuyển đổi status (Ordered | Delivered | Received | Cancelled)
- Search box real-time
- JavaScript AJAX để load data động
- Modal confirm trước khi hủy đơn

## Data Model Cần Có

**PurchaseOrder:**
- PurchaseOrderId
- SupplierId
- WarehouseId
- ExpectedReceivingDate
- Status
- CreatedBy
- CreatedAt

**PurchaseOrderDetail:**
- ProductId
- Quantity
- UnitPrice
- TotalPrice

**Quan hệ:**
- PurchaseOrder → Supplier (nhiều-1)
- PurchaseOrder → Warehouse (nhiều-1)
- PurchaseOrder → User (CreatedBy)
- PurchaseOrder → PurchaseOrderDetails (1-nhiều)
- PurchaseOrderDetail → Product (nhiều-1)

## Gợi Ý Implement

### Service Method
```
GetPurchaseOrdersAsync(warehouseId, status)
→ Return danh sách PurchaseOrder với includes: Supplier, Details, CreatedByUser
```

### Repository Method
```
GetByWarehouseIdAsync(warehouseId, status)
→ Query với Include relationships
→ Filter theo status nếu có
→ Order by CreatedAt descending
```

### View Pattern
```
- Header: Title + Create button
- Filter: Status tabs + Search box
- Table: Responsive với các columns cần thiết
- Actions: View, Cancel buttons
```

## Status Flow
```
Ordered → Delivered → (Staff nhập kho) → Received
   ↓
Cancelled
```

## Tham Khảo
- Xem các controller khác trong project để học pattern
- Sử dụng Bootstrap 5 cho UI
- Icon: Bootstrap Icons
- AJAX: jQuery
