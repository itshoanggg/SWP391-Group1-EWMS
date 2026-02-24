# GIẢI THÍCH FULL LUỒNG LOCATION FILTERING - STOCKIN DETAILS

## TỔNG QUAN
Khi nhập kho, mỗi sản phẩm có thể được phân bổ vào nhiều vị trí khác nhau. Mỗi vị trí có sức chứa giới hạn (maxCapacity). Hệ thống phải đảm bảo:
- Không cho phép nhập vượt quá sức chứa của vị trí
- Khi một vị trí đã ĐẦY, nó sẽ BIẾN MẤT khỏi dropdown ở các dòng khác

---

## CẤU TRÚC DỮ LIỆU

### 1. Biến Global `receiptItems` (Array)
Lưu trữ tất cả các dòng chi tiết nhập kho hiện tại
```javascript
receiptItems = [
    {
        rowId: 1,
        productId: 101,
        productName: "Sản phẩm A",
        locationId: 5,
        receivedQty: 50,
        purchaseOrderDetailId: 1
    },
    {
        rowId: 2,
        productId: 101,
        productName: "Sản phẩm A", 
        locationId: 7,
        receivedQty: 30,
        purchaseOrderDetailId: 1
    },
    ...
]
```

### 2. Biến `locations` (từ server)
Danh sách vị trí với thông tin sức chứa
```javascript
locations = [
    {
        locationId: 5,
        locationCode: "A-01-01",
        maxCapacity: 100,
        availableCapacity: 100  // Ban đầu = maxCapacity
    },
    {
        locationId: 7,
        locationCode: "A-01-02",
        maxCapacity: 50,
        availableCapacity: 50
    },
    ...
]
```

---

## LUỒNG CHẠY CHI TIẾT

### 🎬 **BƯỚC 1: KHỞI TẠO TRANG (Page Load)**

**File:** `Details.cshtml` (dòng 152-168)

```javascript
// Khi trang load xong
document.addEventListener('DOMContentLoaded', function () {
    // 1. Parse dữ liệu từ server
    receiptItems = @Html.Raw(Json.Serialize(Model.Items));
    locations = @Html.Raw(Json.Serialize(Model.Locations));
    
    // 2. Render các dòng chi tiết đã có
    receiptItems.forEach(item => {
        addReceiptItemRow(item);
    });
    
    // 3. Attach event handlers
    attachEventHandlers();
});
```

**Kết quả:** Trang hiển thị các dòng chi tiết từ database

---

### 🎬 **BƯỚC 2: RENDER MỖI DÒNG CHI TIẾT**

**Function:** `addReceiptItemRow(item)` (dòng 88-135)

```javascript
function addReceiptItemRow(item) {
    const rowId = item.rowId || nextRowId++;
    item.rowId = rowId;
    
    // 1. Tạo HTML row
    const row = `
        <tr id="row-${rowId}" data-row-id="${rowId}">
            <td>${item.productName}</td>
            <td>
                <!-- QUAN TRỌNG: Dropdown location -->
                <select id="location-${rowId}" class="form-select location-select">
                    <!-- Sẽ được populate sau -->
                </select>
            </td>
            <td>
                <input type="number" id="qty-${rowId}" value="${item.receivedQty}">
            </td>
            <td>
                <span id="available-${rowId}">-</span>
            </td>
            <td>
                <button onclick="removeRow(${rowId})">Xóa</button>
            </td>
        </tr>
    `;
    
    // 2. Thêm row vào table
    $('#receiptItemsBody').append(row);
    
    // 3. GỌI HÀM QUAN TRỌNG: Populate location dropdown
    populateLocationSelect(rowId, locations);
    
    // 4. Nếu đã có location được chọn trước đó, set lại
    if (item.locationId) {
        $(`#location-${rowId}`).val(item.locationId);
        updateAvailableCapacity(rowId);
    }
}
```

**Kết quả:** Mỗi dòng được thêm vào table với dropdown location rỗng

---

### 🎬 **BƯỚC 3: POPULATE LOCATION DROPDOWN (LOGIC CHÍNH)**

**Function:** `populateLocationSelect(rowId, locations, excludedLocationId)` (dòng 196-258)

Đây là **FUNCTION QUAN TRỌNG NHẤT** - xử lý logic lọc vị trí

```javascript
function populateLocationSelect(rowId, locations, excludedLocationId = null) {
    const select = document.getElementById(`location-${rowId}`);
    if (!select) return;
    
    // ===== BƯỚC 3.1: LẤY THÔNG TIN DÒNG HIỆN TẠI =====
    const item = receiptItems.find(i => i.rowId === rowId);
    if (!item) return;
    
    const productId = item.productId;
    const currentLocationId = item.locationId;
    
    // ===== BƯỚC 3.2: TÍNH TOÁN SỐ LƯỢNG ĐÃ SỬ DỤNG CHO MỖI VỊ TRÍ =====
    const usedLocations = {};  // Object để track: { locationId: totalQtyUsed }
    
    // Duyệt qua TẤT CẢ các dòng chi tiết
    receiptItems.forEach(item => {
        // CHỈ tính những dòng:
        // 1. Cùng productId (cùng sản phẩm)
        // 2. Có locationId hợp lệ
        // 3. KHÔNG PHẢI dòng hiện tại (rowId khác)
        if (item.productId === productId && 
            item.locationId && 
            !isNaN(item.locationId) && 
            item.rowId !== rowId) {
            
            // Lấy số lượng từ input field (để cập nhật real-time)
            const qtyInput = document.getElementById(`qty-${item.rowId}`);
            const qty = qtyInput ? parseInt(qtyInput.value) || 0 : item.receivedQty;
            
            // Cộng dồn số lượng đã dùng cho vị trí này
            if (!usedLocations[item.locationId]) {
                usedLocations[item.locationId] = 0;
            }
            usedLocations[item.locationId] += qty;
        }
    });
    
    console.log(`[Row ${rowId}] Used locations:`, usedLocations);
    // VD output: { 5: 50, 7: 30 } nghĩa là vị trí 5 đã dùng 50, vị trí 7 đã dùng 30
    
    // ===== BƯỚC 3.3: LƯU GIÁ TRỊ ĐANG CHỌN =====
    const currentValue = select.value;
    
    // ===== BƯỚC 3.4: XÓA TẤT CẢ OPTIONS CŨ =====
    select.innerHTML = '<option value="">-- Chọn vị trí --</option>';
    
    // ===== BƯỚC 3.5: LỌC VÀ THÊM CÁC VỊ TRÍ KHẢ DỤNG =====
    locations.forEach(location => {
        // ĐIỀU KIỆN LỌC 1: Bỏ qua vị trí bị exclude
        if (excludedLocationId && location.locationId === excludedLocationId) {
            return;
        }
        
        // LẤY SỨC CHỨA CƠ BẢN từ database
        const baseAvailable = location.availableCapacity || location.maxCapacity || 0;
        
        // LẤY SỐ LƯỢNG ĐÃ DÙNG cho vị trí này
        const usedQty = usedLocations[location.locationId] || 0;
        
        // TÍNH SỨC CHỨA THỰC TẾ CÒN LẠI
        let actualAvailable = baseAvailable - usedQty;
        
        // ĐẶC BIỆT: Nếu đây là vị trí đang được chọn, cộng lại số lượng của dòng này
        if (currentLocationId && location.locationId === currentLocationId) {
            const currentQtyInput = document.getElementById(`qty-${rowId}`);
            const currentQty = currentQtyInput ? parseInt(currentQtyInput.value) || 0 : 0;
            actualAvailable += currentQty;
        }
        
        console.log(`[Location ${location.locationCode}] Base: ${baseAvailable}, Used: ${usedQty}, Actual: ${actualAvailable}`);
        
        // ===== ĐIỀU KIỆN LỌC CHÍNH =====
        // CHỈ THÊM VỊ TRÍ VÀO DROPDOWN NẾU:
        // 1. Còn chỗ trống (actualAvailable > 0) HOẶC
        // 2. Là vị trí đang được chọn
        if (actualAvailable > 0 || location.locationId === currentLocationId) {
            const option = document.createElement('option');
            option.value = location.locationId;
            option.textContent = `${location.locationCode} (Còn: ${actualAvailable})`;
            option.dataset.available = actualAvailable;
            select.appendChild(option);
        }
        // NGƯỢC LẠI: Vị trí bị BỎ QUA (không thêm vào dropdown)
        // => Người dùng sẽ KHÔNG THẤY vị trí này trong danh sách
    });
    
    // ===== BƯỚC 3.6: RESTORE GIÁ TRỊ ĐANG CHỌN =====
    if (currentValue) {
        select.value = currentValue;
    }
}
```

**VÍ DỤ CỤ THỂ:**

Giả sử:
- Sản phẩm A cần nhập 100 cái
- Có 3 vị trí:
  - A-01-01: maxCapacity = 50
  - A-01-02: maxCapacity = 30
  - A-01-03: maxCapacity = 40

**Trạng thái ban đầu:**
- Dòng 1: Chọn A-01-01, nhập 50 cái
- Dòng 2: Chọn A-01-02, nhập 30 cái
- Thêm dòng 3 mới

**Khi populate dropdown cho dòng 3:**

```
usedLocations = {
    A-01-01: 50,  // Dòng 1 đã dùng hết
    A-01-02: 30   // Dòng 2 đã dùng hết
}

Duyệt qua locations:
  - A-01-01: baseAvailable=50, usedQty=50, actualAvailable=0 
    => KHÔNG THÊM vào dropdown (actualAvailable <= 0)
  
  - A-01-02: baseAvailable=30, usedQty=30, actualAvailable=0
    => KHÔNG THÊM vào dropdown
  
  - A-01-03: baseAvailable=40, usedQty=0, actualAvailable=40
    => THÊM vào dropdown

KẾT QUẢ: Dropdown dòng 3 chỉ có A-01-03
```

---

### 🎬 **BƯỚC 4: NGƯỜI DÙNG CHỌN VỊ TRÍ**

**Event Handler:** Khi người dùng click chọn location trong dropdown

**Function:** `handleLocationChange(select)` (dòng 280-309)

```javascript
function handleLocationChange(select) {
    const rowId = parseInt(select.id.split('-')[1]);
    const item = receiptItems.find(i => i.rowId === rowId);
    
    if (!item) return;
    
    // ===== LƯU VỊ TRÍ CŨ =====
    const oldLocationId = item.locationId;
    
    // ===== CẬP NHẬT VỊ TRÍ MỚI =====
    item.locationId = parseInt(select.value) || null;
    
    // ===== CẬP NHẬT HIỂN THỊ SỨC CHỨA CÒN LẠI =====
    updateAvailableCapacity(rowId);
    
    // ===== QUAN TRỌNG: REFRESH TẤT CẢ DROPDOWN =====
    // Nếu vị trí thay đổi, cần refresh lại dropdown của TẤT CẢ dòng cùng sản phẩm
    if (oldLocationId !== item.locationId) {
        refreshAllLocationSelects(item.productId);
    }
}
```

**Tại sao phải refresh tất cả?**
- Vì khi bạn chọn một vị trí mới, sức chứa của vị trí đó giảm
- Các dòng khác cần cập nhật lại để biết vị trí còn bao nhiêu chỗ trống
- Có thể vị trí đó từ "còn chỗ" thành "đầy" => biến mất khỏi dropdown các dòng khác

---

### 🎬 **BƯỚC 5: REFRESH TẤT CẢ LOCATION DROPDOWNS**

**Function:** `refreshAllLocationSelects(productId)` (dòng 260-278)

```javascript
function refreshAllLocationSelects(productId) {
    // Duyệt qua TẤT CẢ các dòng
    receiptItems.forEach(item => {
        // Chỉ refresh những dòng có cùng productId
        if (item.productId === productId) {
            // Gọi lại populateLocationSelect để tính toán lại
            populateLocationSelect(item.rowId, locations);
            
            // Nếu dòng này đã có location, đảm bảo giữ nguyên lựa chọn
            if (item.locationId) {
                const select = document.getElementById(`location-${item.rowId}`);
                if (select) {
                    select.value = item.locationId;
                }
            }
        }
    });
}
```

**VÍ DỤ REFRESH:**

Trạng thái trước refresh:
```
Dòng 1: A-01-01 (50/50) - dropdown có [A-01-01, A-01-02, A-01-03]
Dòng 2: Chưa chọn - dropdown có [A-01-02, A-01-03]
```

User chọn A-01-02 cho dòng 2 với số lượng 30:
```
Trigger: handleLocationChange
=> Call: refreshAllLocationSelects(productId)
=> Populate lại dropdown dòng 1 và 2

Sau refresh:
Dòng 1: A-01-01 (50/50) - dropdown chỉ còn [A-01-01] (vì A-01-02 đầy, A-01-03 còn)
Dòng 2: A-01-02 (30/30) - dropdown chỉ còn [A-01-02, A-01-03]
```

---

### 🎬 **BƯỚC 6: NGƯỜI DÙNG THAY ĐỔI SỐ LƯỢNG**

**Event Handler:** `change` event trên input số lượng

**Function:** `handleQuantityChange(input)` (dòng 311-336)

```javascript
function handleQuantityChange(input) {
    const rowId = parseInt(input.id.split('-')[1]);
    const item = receiptItems.find(i => i.rowId === rowId);
    
    if (!item) return;
    
    const newQty = parseInt(input.value) || 0;
    const locationId = item.locationId;
    
    if (!locationId) {
        alert('Vui lòng chọn vị trí trước');
        input.value = item.receivedQty;
        return;
    }
    
    // ===== KIỂM TRA SỨC CHỨA =====
    const location = locations.find(l => l.locationId === locationId);
    if (!location) return;
    
    const maxCapacity = location.maxCapacity || 0;
    
    // Tính số lượng đã dùng ở vị trí này (KHÔNG tính dòng hiện tại)
    let usedQty = 0;
    receiptItems.forEach(i => {
        if (i.productId === item.productId && 
            i.locationId === locationId && 
            i.rowId !== rowId) {
            const qtyInput = document.getElementById(`qty-${i.rowId}`);
            usedQty += qtyInput ? parseInt(qtyInput.value) || 0 : 0;
        }
    });
    
    const availableForThisRow = maxCapacity - usedQty;
    
    // ===== VALIDATE =====
    if (newQty > availableForThisRow) {
        alert(`Vị trí chỉ còn ${availableForThisRow} chỗ trống!`);
        input.value = availableForThisRow;
        item.receivedQty = availableForThisRow;
    } else {
        item.receivedQty = newQty;
    }
    
    // ===== CẬP NHẬT HIỂN THỊ =====
    updateAvailableCapacity(rowId);
    
    // ===== QUAN TRỌNG: REFRESH DROPDOWN =====
    // Vì số lượng thay đổi => sức chứa còn lại thay đổi
    refreshAllLocationSelects(item.productId);
}
```

**VÍ DỤ:**
```
Vị trí A-01-01: maxCapacity = 100
Dòng 1: A-01-01, qty = 60
Dòng 2: A-01-01, qty = 30

User thay đổi dòng 1 từ 60 → 50:
=> usedQty (của dòng 2) = 30
=> availableForThisRow = 100 - 30 = 70
=> 50 <= 70 => OK
=> Cập nhật receivedQty = 50
=> Refresh dropdown tất cả dòng
   => Dòng 2 thấy còn 50 chỗ trống
```

---

### 🎬 **BƯỚC 7: THÊM DÒNG MỚI**

**Function:** `addNewReceiptItem()` (dòng 137-194)

```javascript
function addNewReceiptItem() {
    const productSelect = document.getElementById('productSelect');
    const qtyInput = document.getElementById('newItemQty');
    
    const productId = parseInt(productSelect.value);
    const qty = parseInt(qtyInput.value);
    
    // Validate...
    
    // ===== TẠO ITEM MỚI =====
    const newItem = {
        rowId: nextRowId++,
        productId: productId,
        productName: productSelect.options[productSelect.selectedIndex].text,
        locationId: null,  // Chưa chọn vị trí
        receivedQty: qty,
        purchaseOrderDetailId: parseInt(productSelect.options[productSelect.selectedIndex].dataset.poDetailId)
    };
    
    // ===== THÊM VÀO ARRAY =====
    receiptItems.push(newItem);
    
    // ===== RENDER ROW =====
    addReceiptItemRow(newItem);
    // => Gọi populateLocationSelect bên trong
    // => Dropdown sẽ KHÔNG có những vị trí đã đầy
    
    // Reset form...
}
```

---

## TÓM TẮT LUỒNG HOẠT ĐỘNG

```
1. PAGE LOAD
   └─> Parse dữ liệu từ server
   └─> Render các dòng chi tiết
       └─> Mỗi dòng: addReceiptItemRow()
           └─> populateLocationSelect() ← TÍNH TOÁN LẦN ĐẦU

2. USER CHỌN LOCATION
   └─> handleLocationChange()
       └─> Cập nhật item.locationId
       └─> refreshAllLocationSelects() ← TÍNH TOÁN LẠI TẤT CẢ
           └─> populateLocationSelect() cho mỗi dòng

3. USER THAY ĐỔI SỐ LƯỢNG
   └─> handleQuantityChange()
       └─> Validate sức chứa
       └─> Cập nhật item.receivedQty
       └─> refreshAllLocationSelects() ← TÍNH TOÁN LẠI TẤT CẢ
           └─> populateLocationSelect() cho mỗi dòng

4. USER THÊM DÒNG MỚI
   └─> addNewReceiptItem()
       └─> Tạo item mới
       └─> addReceiptItemRow()
           └─> populateLocationSelect() ← TÍNH TOÁN CHO DÒNG MỚI
               └─> Vị trí đầy sẽ KHÔNG XUẤT HIỆN
```

---

## CÔNG THỨC TÍNH TOÁN CHÍNH

```
Cho mỗi vị trí L và dòng hiện tại R:

1. baseAvailable = L.availableCapacity (từ database)

2. usedQty = Σ(qty của các dòng khác cùng productId và cùng locationId)

3. actualAvailable = baseAvailable - usedQty

4. Nếu L là vị trí đang chọn của R:
   actualAvailable += qty của R (để không tự loại mình)

5. ĐIỀU KIỆN HIỂN THỊ:
   if (actualAvailable > 0 OR L là vị trí đang chọn):
       THÊM L vào dropdown
   else:
       BỎ QUA (không thêm vào dropdown)
```

---

## ĐIỂM QUAN TRỌNG CẦN NHỚ

1. **Mỗi lần có thay đổi** (chọn vị trí, đổi số lượng) → **PHẢI refresh tất cả dropdown**

2. **Tính actualAvailable**: Phải cộng lại qty của chính dòng đó nếu đang chọn vị trí này

3. **Real-time calculation**: Luôn lấy giá trị từ input field, không dùng cached value

4. **Filter logic**: `actualAvailable > 0` hoặc `là vị trí đang chọn`

5. **Scope**: Chỉ filter trong cùng sản phẩm (cùng productId)

---

## DEBUG TIPS

Để debug, mở Console và xem log:
```javascript
console.log(`[Row ${rowId}] Used locations:`, usedLocations);
console.log(`[Location ${location.locationCode}] Base: ${baseAvailable}, Used: ${usedQty}, Actual: ${actualAvailable}`);
```

Sẽ thấy output như:
```
[Row 2] Used locations: {5: 50, 7: 30}
[Location A-01-01] Base: 100, Used: 50, Actual: 50
[Location A-01-02] Base: 50, Used: 30, Actual: 20
[Location A-01-03] Base: 50, Used: 0, Actual: 50
```

---

## KẾT LUẬN

Logic này đảm bảo:
✅ Không nhập vượt sức chứa vị trí
✅ Vị trí đầy sẽ tự động biến mất khỏi dropdown
✅ Cập nhật real-time khi thay đổi số lượng
✅ Mỗi sản phẩm có logic riêng (không ảnh hưởng lẫn nhau)
✅ UX tốt: không cho user chọn vị trí không hợp lệ

