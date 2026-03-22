# Yêu Cầu: Xem Chi Tiết Đơn Mua Hàng (Purchase Order Details)

## Mô Tả Chức Năng
Purchasing Staff xem thông tin chi tiết của một đơn mua hàng đã tạo.

## Yêu Cầu Nghiệp Vụ

### Hiển Thị

**Thông tin đơn hàng:**
- Mã đơn (PO-0001)
- Trạng thái (badge với màu)
- Nhà cung cấp (tên, SĐT, email, địa chỉ)
- Ngày tạo đơn
- Ngày dự kiến nhận hàng
- Người tạo đơn

**Bảng sản phẩm:**
- STT
- Tên sản phẩm
- Đơn vị tính
- Số lượng
- Đơn giá
- Thành tiền

**Tổng cộng:**
- Tổng số lượng
- Tổng giá trị đơn hàng

### Actions
- Nút "Back" về Index
- Nút "Edit" (nếu status = Ordered)
- Nút "Cancel" (nếu status = Ordered)
- Nút "Mark as Delivered" (nếu status = Ordered)

## Yêu Cầu Kỹ Thuật

### Backend

**GET Details/{id}:**
- Load PurchaseOrder by ID
- Include: Supplier, Details (with Product), CreatedBy user
- Check user có quyền xem không (cùng warehouse)
- Tính totals
- Return View

### Frontend

**Layout:**
```
┌─────────────────────────────┐
│ Header (PO-0001, Status)    │
├─────────────────────────────┤
│ Supplier Info Card          │
├─────────────────────────────┤
│ Order Info (dates, creator) │
├─────────────────────────────┤
│ Products Table              │
│ ├─ Product 1                │
│ ├─ Product 2                │
│ └─ ...                      │
├─────────────────────────────┤
│ Totals                      │
├─────────────────────────────┤
│ Action Buttons              │
└─────────────────────────────┘
```

**Status Badge Colors:**
- Ordered: warning (yellow)
- Delivered: info (blue)
- Received: success (green)
- Cancelled: danger (red)

## Business Logic

### Edit Rules
- Chỉ edit được khi status = "Ordered"
- Redirect to Edit page

### Cancel Rules
- Chỉ cancel được khi status = "Ordered"
- Confirm trước khi cancel
- Update status → "Cancelled"

### Mark as Delivered
- Chỉ cho Ordered
- Update status → "Delivered"
- (Note: Thực tế có thể không cần action này nếu dùng auto-update)

## Gợi Ý Implement

### Service
```
GetPurchaseOrderByIdAsync(id, warehouseId)
→ Return PurchaseOrder with all includes
→ Null nếu không tìm thấy hoặc không có quyền
```

### View
```
@model PurchaseOrder

<div class="card">
  <div class="card-header">
    <h3>PO-@Model.PurchaseOrderId.ToString("D4")</h3>
    <span class="badge">@Model.Status</span>
  </div>
  
  <div class="card-body">
    <!-- Supplier info -->
    <!-- Order info -->
    <!-- Products table -->
  </div>
  
  <div class="card-footer">
    <!-- Buttons -->
  </div>
</div>
```

## Lưu Ý
- Read-only page, không có form edit ở đây
- Security: check user warehouse
- Format số tiền: N2 (2 decimals)
- Format date: dd/MM/yyyy
