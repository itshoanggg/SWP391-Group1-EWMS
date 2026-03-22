# Yêu Cầu: Báo Cáo Nhập-Xuất-Tồn (NXT Report)

## Mô Tả Chức Năng
Warehouse Manager cần xem báo cáo tồn kho theo kỳ: Tồn đầu, Nhập trong kỳ, Xuất trong kỳ, Tồn cuối cho từng sản phẩm.

## Yêu Cầu Nghiệp Vụ

### Thông Tin Báo Cáo

**Header:**
- Tên kho
- Kỳ báo cáo (từ ngày - đến ngày)

**Summary Cards:**
- Tổng tồn đầu kỳ
- Tổng nhập trong kỳ
- Tổng xuất trong kỳ
- Tổng tồn cuối kỳ (+ chênh lệch vs đầu kỳ)

**Bảng Chi Tiết:**
Mỗi dòng là 1 sản phẩm với:
- Tên sản phẩm
- Đơn vị tính
- Tồn đầu kỳ
- Nhập trong kỳ (số lượng + giá trị)
- Xuất trong kỳ (số lượng + giá trị)
- Tồn cuối kỳ (số lượng + giá trị)
- Badge "Low" nếu tồn < ngưỡng

### Filter
- Chọn kho (nếu user quản lý nhiều kho)
- Chọn tháng/năm (month picker)
- Hoặc chọn kỳ: tháng này, quý này, năm này

### Export
- Export Excel (CSV)
- Export PDF (print)

## Yêu Cầu Kỹ Thuật

### Backend

**Controller Action:**
```
GET /Reports/NXT?warehouseId=1&year=2024&month=3
```

**Tham số:**
- warehouseId (optional, default = user's warehouse)
- period (month/quarter/year)
- year, month (optional cho custom selection)
- print (bool, cho print mode)

**Business Logic - Cách Tính:**

Cho mỗi sản phẩm:

1. **Tồn cuối hiện tại** = Sum(Inventory.Quantity) bây giờ

2. **Nhập/Xuất TRONG kỳ** [from → to]:
   - InQty = Sum(StockInDetails.Quantity) where ReceivedDate trong [from, to]
   - OutQty = Sum(StockOutDetails.Quantity) where IssuedDate trong [from, to]

3. **Nhập/Xuất SAU kỳ** (sau 'to'):
   - InAfter = Sum(StockInDetails) sau 'to'
   - OutAfter = Sum(StockOutDetails) sau 'to'

4. **Tồn cuối kỳ**:
   ```
   EndQty = CurrentQty - InAfter + OutAfter
   ```

5. **Tồn đầu kỳ**:
   ```
   BeginQty = EndQty - InQty + OutQty
   ```

6. **Giá trị**:
   ```
   EndValue = EndQty × CostPrice
   InValue = Sum(StockInDetails.TotalPrice) trong kỳ
   OutValue = Sum(StockOutDetails.TotalPrice) trong kỳ
   ```

### Frontend

**View:**
- 4 stat cards ở đầu
- Filter form (warehouse, month picker, buttons)
- Table với grouped columns (In, Out, Stock)
- Footer totals
- Export buttons

**CSS:**
- Stat cards: shadow, rounded corners
- Table: colored headers (green for in, red for out, blue for stock)
- Print mode: ẩn filter bar, background trắng
- Responsive

**JavaScript:**
- Month picker: HTML5 input type="month"
- Sync với hidden inputs (year, month)
- Auto-submit khi đổi warehouse

## ViewModels

```
NXTReportViewModel {
    WarehouseId
    WarehouseName
    FromDate
    ToDate
    Rows: List<NXTReportRow>
    Totals: NXTReportTotals
    DeltaQty (computed)
}

NXTReportRow {
    ProductId
    ProductName
    Unit
    BeginQty
    InQty, InValue
    OutQty, OutValue
    EndQty, EndValue
    CostPrice
}

NXTReportTotals {
    BeginQty
    InQty, InValue
    OutQty, OutValue
    EndQty, EndValue
}
```

## Export

### Excel (CSV):
```
Product,Unit,Begin Qty,In Qty,Out Qty,End Qty,End Value
ProductA,Pcs,100,50,30,120,1200000
...
TOTAL,,500,200,150,550,5500000
```

### PDF:
- Redirect to NXT?print=true
- Browser print dialog
- CSS @media print

## Gợi Ý Implement

### Service
```
GetNXTReportAsync(warehouseId, fromDate, toDate)
{
    1. Query current inventory
    2. Query movements in period
    3. Query movements after period
    4. Calculate for each product
    5. Calculate totals
    6. Return ViewModel
}
```

### Period Helper
```
ResolvePeriod("month") → (firstDayOfMonth, today)
ResolvePeriod("quarter") → (firstDayOfQuarter, today)
ResolvePeriod("year") → (Jan 1, today)
```

## Lưu Ý
- Báo cáo này phức tạp, cần optimize query
- Sử dụng GroupBy để aggregate
- Cache nếu cần (cho period đã qua)
- Performance test với data lớn
