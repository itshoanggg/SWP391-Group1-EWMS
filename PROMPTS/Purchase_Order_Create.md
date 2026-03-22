# Yêu Cầu: Tạo Đơn Mua Hàng (Purchase Order Create)

## Mô Tả Chức Năng
Purchasing Staff cần tạo đơn đặt hàng mới với nhà cung cấp, chọn nhiều sản phẩm với số lượng và giá.

## Yêu Cầu Nghiệp Vụ

### Form Cần Có
**Thông tin chính:**
- Chọn nhà cung cấp (dropdown)
- Ngày dự kiến nhận hàng (date picker, mặc định +7 ngày)
- Danh sách sản phẩm (dynamic table)

**Bảng sản phẩm:**
- Chọn sản phẩm (dropdown theo supplier)
- Nhập số lượng
- Nhập đơn giá
- Tự động tính thành tiền
- Nút thêm/xóa dòng
- Hiển thị tổng cộng

### Validation
- Bắt buộc chọn supplier
- Ít nhất 1 sản phẩm
- Số lượng > 0
- Đơn giá > 0
- Không cho chọn trùng sản phẩm (hoặc merge nếu trùng)

### Sau Khi Tạo
- Lưu vào database
- Status = "Ordered"
- Ghi nhận người tạo (CreatedBy)
- Redirect về Details page
- Hiển thị message thành công

## Yêu Cầu Kỹ Thuật

### Backend

**GET Create:**
- Load danh sách suppliers vào dropdown
- Pass warehouse ID và user ID vào ViewBag

**POST Create:**
- Nhận ViewModel từ form
- Validate dữ liệu
- Tạo PurchaseOrder
- Tạo PurchaseOrderDetails
- Handle duplicate products (merge quantities)
- Sử dụng transaction
- Return về Details nếu thành công

**API Hỗ Trợ:**
- GET `/PurchaseOrder/GetProductsBySupplier?supplierId=1`
  → Trả về danh sách products của supplier đó với cost price
- GET `/PurchaseOrder/GetSupplierInfo?supplierId=1`
  → Trả về thông tin supplier (name, phone, address)

### Frontend

**JavaScript:**
- Thêm/xóa dòng sản phẩm động
- Load products khi chọn supplier
- Tự động điền giá khi chọn product
- Tính thành tiền khi nhập số lượng/giá
- Tính tổng cộng realtime
- Validation client-side
- Submit qua form hoặc AJAX

**UI/UX:**
- Form rõ ràng, dễ nhập liệu
- Disable submit nếu form invalid
- Loading indicator khi submit
- Hiển thị thông tin supplier khi chọn

## ViewModel

```
PurchaseOrderCreateViewModel
{
    SupplierId: int
    ExpectedReceivingDate: DateTime?
    Details: List<PurchaseOrderDetailViewModel>
}

PurchaseOrderDetailViewModel  
{
    ProductId: int
    Quantity: int
    UnitPrice: decimal
}
```

## Business Logic

### Tính TotalPrice
```
TotalPrice = Quantity × UnitPrice
```

### Merge Duplicate Products
Nếu user chọn cùng 1 product nhiều lần:
- Cộng dồn số lượng
- Lấy giá của lần chọn đầu tiên

### Default Values
- ExpectedReceivingDate = Today + 7 days
- Status = "Ordered"
- CreatedAt = Now
- CreatedBy = Current User ID
- WarehouseId = User's assigned warehouse

## Gợi Ý Implement

### Service
```
CreatePurchaseOrderAsync(model, warehouseId, userId)
{
    1. Create PurchaseOrder entity
    2. Save to get ID
    3. Group details by ProductId (handle duplicates)
    4. Create PurchaseOrderDetail entities
    5. Save changes
    6. Return created PurchaseOrder
}
```

### JavaScript Pattern
```
- Array để lưu danh sách products đã chọn
- Function addProductRow()
- Function removeProductRow(index)
- Function calculateRowTotal()
- Function calculateGrandTotal()
- Event listeners cho các inputs
```

## Lưu Ý
- Chỉ được chọn products của supplier đã chọn
- Cost price mặc định từ product, nhưng cho phép sửa
- Form phải dễ dùng, nhập liệu nhanh
- Handle errors gracefully
