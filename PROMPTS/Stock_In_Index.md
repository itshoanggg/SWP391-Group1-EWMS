# Yêu Cầu: Trang Nhập Kho (Stock In Index)

## Mô Tả Chức Năng
Inventory Staff xem danh sách Purchase Orders cần nhập kho và thực hiện nhập hàng vào các vị trí trong kho.

## Yêu Cầu Nghiệp Vụ

### Ai sử dụng?
- Inventory Staff hoặc Warehouse Manager
- Chỉ làm việc với kho được phân công

### Hiển thị gì?
Danh sách Purchase Orders với:
- Mã đơn hàng (PO-0001)
- Nhà cung cấp
- Ngày dự kiến nhận
- Tổng số lượng đặt
- Đã nhập bao nhiêu
- Còn lại bao nhiêu
- Trạng thái (Ordered, PartiallyReceived)
- Nút "Nhập kho"

### Tương Tác
- Xem danh sách đơn hàng cần nhập kho (status: Ordered, PartiallyReceived)
- Tìm kiếm theo mã đơn hoặc supplier
- Click "Nhập kho" → Đi đến Stock In Details page

## Yêu Cầu Kỹ Thuật

### Backend
- Controller: StockInController
- Action: Index (GET)
- Authorize: "Inventory Staff,Warehouse Manager"

**Luồng:**
1. Check user authentication
2. Get user's warehouse
3. Load PurchaseOrders với status = Ordered hoặc PartiallyReceived
4. Trả về View

**API:**
- GET `/StockIn/GetPurchaseOrders?warehouseId=1&status=Ordered&search=`
  → Return danh sách PO cần nhập kho

### Frontend
- Simple table với search
- Button "Nhập kho" link đến Details page
- Badge hiển thị trạng thái
- Progress bar cho đơn đã nhập một phần

## Business Logic

### Purchase Order Status
- **Ordered**: Chưa nhập gì
- **PartiallyReceived**: Đã nhập 1 phần (chưa đủ)
- **Received**: Đã nhập đủ

### Tính Toán
```
TotalOrdered = Sum(PurchaseOrderDetails.Quantity)
AlreadyReceived = Sum(StockInDetails where PurchaseOrderId = x)
Remaining = TotalOrdered - AlreadyReceived
```

## Gợi Ý Implement

### Service Method
```
GetPurchaseOrdersForStockInAsync(warehouseId, status, search)
→ Filter POs có status = Ordered hoặc PartiallyReceived
→ Include Supplier, Details
→ Tính tổng đã nhận và còn lại
```

### View
```
- Header
- Search box
- Table: PO info + Progress + Actions
- Link đến Details page: /StockIn/Details/{id}
```

## Lưu Ý
- Chỉ hiển thị PO của warehouse user được assign
- Không cho edit/delete PO (đó là việc của Purchasing Staff)
- Focus vào việc nhập kho
