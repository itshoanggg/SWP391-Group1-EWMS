/* =========================================================
   GOODS RECEIPT - MAIN LOGIC
========================================================= */

let productsData = [];
let locationsCache = {};
let receiptItems = []; // Lưu trữ tất cả các dòng nhập kho
let locationUsage = {};
// Format helpers
function formatNumber(value) {
    return new Intl.NumberFormat('vi-VN').format(value);
}

/* =========================================================
   LOAD DATA
========================================================= */
async function loadPurchaseOrderInfo() {
    try {
        const response = await fetch(`/StockIn/GetPurchaseOrderInfo?purchaseOrderId=${purchaseOrderId}`);
        const data = await response.json();

        if (data.error) {
            console.error('Error:', data.error);
            return;
        }

        // Update UI with PO info
        document.getElementById('po-number').textContent = `PO-${String(data.purchaseOrderId).padStart(4, '0')}`;
        document.getElementById('supplier-name').textContent = data.supplierName;
        document.getElementById('supplier-code').textContent = `NCC-${data.purchaseOrderId}`;
        document.getElementById('supplier-phone').textContent = data.supplierPhone || 'N/A';

        // Check if already received
        if (data.hasStockIn) {
            alert('Đơn hàng này đã được nhập kho!');
            document.getElementById('btn-confirm').disabled = true;
        }
    } catch (error) {
        console.error('Load PO info failed:', error);
    }
}

async function loadProducts() {
    try {
        const response = await fetch(`/StockIn/GetPurchaseOrderProducts?purchaseOrderId=${purchaseOrderId}`);
        const data = await response.json();

        if (data.error) {
            console.error('Error:', data.error);
            return;
        }

        productsData = data;
        renderProductsTable();
        updateSummary();
    } catch (error) {
        console.error('Load products failed:', error);
    }
}

/* =========================================================
   RENDER PRODUCTS TABLE
========================================================= */
function renderProductsTable() {
    const tbody = document.getElementById('products-tbody');
    tbody.innerHTML = '';

    productsData.forEach((product, index) => {
        const rowId = `product-${product.productId}-${index}`;

        const row = document.createElement('tr');
        row.id = rowId;
        row.innerHTML = `
            <td>
                <div class="sku-badge">${product.sku}</div>
            </td>
            <td>
                <div class="product-name">${product.productName}</div>
                <small class="text-muted">${product.categoryName}</small>
            </td>
            <td>
                <span class="badge bg-secondary">${formatNumber(product.orderedQty)}</span>
            </td>
            <td>
                <input type="number" 
                       class="form-control qty-input" 
                       id="qty-${rowId}"
                       data-product-id="${product.productId}"
                       data-row-id="${rowId}"
                       value="${product.orderedQty}"
                       min="0"
                       max="${product.orderedQty}"
                       onchange="handleQuantityChange(this)">
            </td>
            <td>
                <select class="form-select location-select" 
                        id="location-${rowId}"
                        data-product-id="${product.productId}"
                        data-row-id="${rowId}"
                        onchange="handleLocationChange(this)">
                    <option value="">-- Chọn vị trí --</option>
                </select>
                <div class="location-info mt-1" id="location-info-${rowId}"></div>
            </td>
            <td>
                <button class="btn btn-sm btn-primary" onclick="showLocationModal('${rowId}', ${product.productId})">
                    <i class="fas fa-map-marker-alt"></i> Gọi vị trí
                </button>
            </td>
        `;

        tbody.appendChild(row);

        // Load locations for this product
        loadLocationsForProduct(product.productId, rowId);

        // Initialize receipt item
        receiptItems.push({
            rowId: rowId,
            productId: product.productId,
            productName: product.productName,
            orderedQty: product.orderedQty,
            receivedQty: product.orderedQty,
            locationId: null,
            unitPrice: product.unitPrice,
            splitRows: []
        });
    });
}

/* =========================================================
   LOAD LOCATIONS
========================================================= */
async function loadLocationsForProduct(productId, rowId) {
    try {
        if (locationsCache[productId]) {
            populateLocationSelect(rowId, locationsCache[productId]);
            return;
        }

        const response = await fetch(`/StockIn/GetAvailableLocations?warehouseId=${warehouseId}&productId=${productId}`);
        const data = await response.json();

        if (!data.error) {
            locationsCache[productId] = data;
            populateLocationSelect(rowId, data);
        }
    } catch (error) {
        console.error('Load locations failed:', error);
    }
}

function populateLocationSelect(rowId, locations, excludedLocationId = null) {
    const select = document.getElementById(`location-${rowId}`);
    if (!select) return;

    select.innerHTML = '<option value="">-- Chọn vị trí --</option>';

    locations.forEach(loc => {
        const available = loc.maxCapacity - loc.currentStock;

        // ❌ rack đầy
        if (available <= 0) return;

        // ❌ loại rack cha khi split
        if (excludedLocationId && loc.locationId === excludedLocationId) return;

        const option = document.createElement('option');
        option.value = loc.locationId;
        option.textContent =
            `${loc.locationCode} - ${loc.locationName} (${loc.currentStock}/${loc.maxCapacity})`;

        option.dataset.available = available;
        select.appendChild(option);
    });
}



/* =========================================================
   HANDLE QUANTITY CHANGE
========================================================= */
function handleQuantityChange(input) {
    const rowId = input.dataset.rowId;
    const productId = parseInt(input.dataset.productId);
    const newQty = parseInt(input.value) || 0;

    // Find and update receipt item
    const item = receiptItems.find(i => i.rowId === rowId);
    if (item) {
        item.receivedQty = newQty;
    }

    updateSummary();
    checkCapacity(rowId, newQty);
}

/* =========================================================
   HANDLE LOCATION CHANGE
========================================================= */
function handleLocationChange(select) {
    const rowId = select.dataset.rowId;
    const locationId = parseInt(select.value);
    const qtyInput = document.getElementById(`qty-${rowId}`);
    const qty = parseInt(qtyInput.value) || 0;

    const item = receiptItems.find(i => i.rowId === rowId);
    if (!item) return;

    item.locationId = locationId || null;

    if (locationId && qty > 0) {
        checkCapacity(rowId, qty);
    } else {
        clearLocationInfo(rowId);
    }
}



/* =========================================================
   CHECK LOCATION CAPACITY
========================================================= */
async function checkCapacity(rowId, quantity) {
    const select = document.getElementById(`location-${rowId}`);
    const locationId = parseInt(select.value);
    const infoDiv = document.getElementById(`location-info-${rowId}`);

    if (!locationId || quantity <= 0) {
        infoDiv.innerHTML = '';
        return;
    }

    const res = await fetch(`/StockIn/CheckLocationCapacity?locationId=${locationId}`);
    const data = await res.json();

    const available = data.maxCapacity - data.currentStock;
    const locationText = select.options[select.selectedIndex].text;

    // ===== ĐỦ CHỖ =====
    if (quantity <= available) {
        infoDiv.innerHTML = `
            <div class="alert alert-success p-2 mb-0">
                ${locationText}<br>
                <b>Còn trống sau nhập: ${available - quantity}</b>
            </div>
        `;
        return;
    }

    // ===== KHÔNG ĐỦ → HIỆN SPLIT =====
    const remain = quantity - available;

    infoDiv.innerHTML = `
        <div class="alert alert-warning p-2 mb-2">
            ${locationText}<br>
            <b>Chỉ chứa được ${available}</b>
        </div>

        <button class="btn btn-sm btn-warning"
            onclick="splitToNewLocation(
                '${rowId}',
                ${parseInt(select.dataset.productId)},
                ${quantity},
                ${available}
            )">
            ➕ Thêm rack khác cho ${remain}
        </button>
    `;
}



function clearLocationInfo(rowId) {
    const infoDiv = document.getElementById(`location-info-${rowId}`);
    if (infoDiv) {
        infoDiv.innerHTML = '';
    }
}

/* =========================================================
   SPLIT TO NEW LOCATION
========================================================= */
function splitToNewLocation(parentRowId, productId, totalQty, firstCapacity) {
    const parentRow = document.getElementById(parentRowId);
    const parentItem = receiptItems.find(i => i.rowId === parentRowId);
    const product = productsData.find(p => p.productId === productId);

    if (!parentRow || !parentItem || !product) return;

    // ===== FIX DÒNG CHA =====
    const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
    parentQtyInput.value = firstCapacity;
    parentQtyInput.max = firstCapacity;
    parentItem.receivedQty = firstCapacity;

    const parentLocationId =
        parseInt(document.getElementById(`location-${parentRowId}`).value);

    const remainingQty = totalQty - firstCapacity;
    if (remainingQty <= 0) return;

    // ===== TẠO DÒNG SPLIT =====
    const newRowId = `split-${Date.now()}`;

    const newRow = document.createElement('tr');
    newRow.id = newRowId;
    newRow.className = 'split-row bg-light';

    newRow.innerHTML = `
        <td><span class="sku-badge bg-warning">${product.sku}</span></td>
        <td>${product.productName}</td>
        <td><span class="badge bg-warning">${remainingQty}</span></td>
        <td>
            <input type="number"
                   id="qty-${newRowId}"
                   class="form-control"
                   value="${remainingQty}"
                   min="1"
                   max="${remainingQty}"
                   data-row-id="${newRowId}"
                   data-product-id="${productId}"
                   onchange="handleQuantityChange(this)">
        </td>
        <td>
            <select id="location-${newRowId}"
                    class="form-select"
                    data-row-id="${newRowId}"
                    data-product-id="${productId}"
                    onchange="handleLocationChange(this)">
                <option value="">-- Chọn rack khác --</option>
            </select>
            <div id="location-info-${newRowId}" class="mt-1"></div>
        </td>
        <td></td>
    `;

    parentRow.after(newRow);

    receiptItems.push({
        rowId: newRowId,
        productId,
        productName: product.productName,
        orderedQty: remainingQty,
        receivedQty: remainingQty,
        locationId: null,
        unitPrice: product.unitPrice,
        isSplit: true,
        parentRowId
    });

    // 🔥 LOAD LOCATION → LOẠI RACK CHA
    populateLocationSelect(
        newRowId,
        locationsCache[productId],
        parentLocationId
    );

    updateSummary();
}


function removeSplitRow(rowId, parentRowId, qty) {
    // Remove row from DOM
    const row = document.getElementById(rowId);
    if (row) {
        row.remove();
    }

    // Remove from receipt items
    const index = receiptItems.findIndex(i => i.rowId === rowId);
    if (index > -1) {
        receiptItems.splice(index, 1);
    }

    // Update parent row
    const parentItem = receiptItems.find(i => i.rowId === parentRowId);
    if (parentItem) {
        const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
        const currentMax = parseInt(parentQtyInput.max);
        parentQtyInput.max = currentMax + qty;

        // Remove from split rows
        if (parentItem.splitRows) {
            parentItem.splitRows = parentItem.splitRows.filter(id => id !== rowId);
        }
    }

    updateSummary();
}

/* =========================================================
   LOCATION MODAL
========================================================= */
function showLocationModal(rowId, productId) {
    const productInfo = productsData.find(p => p.productId === productId);
    const qtyInput = document.getElementById(`qty-${rowId}`);
    const qty = qtyInput.value;

    document.getElementById('modal-product-name').textContent = productInfo.productName;
    document.getElementById('modal-quantity').textContent = qty;

    const tbody = document.getElementById('locations-tbody');
    tbody.innerHTML = '';

    const locations = locationsCache[productId] || [];
    locations.forEach(loc => {
        const row = document.createElement('tr');
        row.style.cursor = 'pointer';
        row.onclick = () => selectLocationFromModal(rowId, loc.locationId);
        row.innerHTML = `
            <td><strong>${loc.locationCode}</strong></td>
            <td>${loc.locationName}</td>
            <td>${loc.rack}</td>
            <td><span class="badge bg-info">${loc.currentStock}</span></td>
            <td>
                <button class="btn btn-sm btn-success" onclick="selectLocationFromModal('${rowId}', ${loc.locationId})">
                    <i class="fas fa-check"></i> Chọn
                </button>
            </td>
        `;
        tbody.appendChild(row);
    });

    const modal = new bootstrap.Modal(document.getElementById('locationModal'));
    modal.show();
}

function selectLocationFromModal(rowId, locationId) {
    const select = document.getElementById(`location-${rowId}`);
    select.value = locationId;
    handleLocationChange(select);

    bootstrap.Modal.getInstance(document.getElementById('locationModal')).hide();
}

/* =========================================================
   UPDATE SUMMARY
========================================================= */
function updateSummary() {
    const totalSku = productsData.length;
    const totalOrdered = productsData.reduce((sum, p) => sum + p.orderedQty, 0);
    const totalReceived = receiptItems.reduce((sum, item) => sum + item.receivedQty, 0);
    const difference = totalOrdered - totalReceived;

    document.getElementById('total-sku').textContent = formatNumber(totalSku);
    document.getElementById('total-ordered').textContent = formatNumber(totalOrdered);
    document.getElementById('total-received').textContent = formatNumber(totalReceived);

    const statusElem = document.getElementById('receipt-status');
    if (difference === 0) {
        statusElem.textContent = 'Đủ hàng';
        statusElem.className = 'text-success';
    } else if (difference > 0) {
        statusElem.textContent = `Thiếu ${formatNumber(difference)} sản phẩm`;
        statusElem.className = 'text-warning';
    } else {
        statusElem.textContent = `Thừa ${formatNumber(Math.abs(difference))} sản phẩm`;
        statusElem.className = 'text-danger';
    }
}

/* =========================================================
   CONFIRM STOCK IN
========================================================= */
async function confirmStockIn() {
    // Validate
    const errors = [];

    for (const item of receiptItems) {
        if (item.receivedQty > 0 && !item.locationId) {
            errors.push(`${item.productName}: Chưa chọn vị trí lưu kho`);
        }
    }

    if (errors.length > 0) {
        alert('Vui lòng kiểm tra:\n\n' + errors.join('\n'));
        return;
    }

    if (!confirm('Xác nhận nhập kho các sản phẩm này?')) {
        return;
    }

    // Prepare data
    const requestData = {
        purchaseOrderId: purchaseOrderId,
        warehouseId: warehouseId,
        items: receiptItems
            .filter(item => item.receivedQty > 0 && item.locationId)
            .map(item => ({
                productId: item.productId,
                locationId: item.locationId,
                quantity: item.receivedQty,
                unitPrice: item.unitPrice
            }))
    };

    try {
        const response = await fetch('/StockIn/ConfirmStockIn', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestData)
        });

        const result = await response.json();

        if (result.success) {
            alert(result.message);
            window.location.href = '/StockIn/Index';
        } else {
            alert('Lỗi: ' + result.error);
        }
    } catch (error) {
        console.error('Confirm stock in failed:', error);
        alert('Có lỗi xảy ra khi nhập kho!');
    }
}

function refreshAllLocationSelects(productId) {
    const locations = locationsCache[productId];
    if (!locations) return;

    receiptItems.forEach(item => {
        if (item.productId === productId) {
            populateLocationSelect(item.rowId, locations);

            // giữ lại lựa chọn cũ nếu còn hợp lệ
            if (item.locationId) {
                const select = document.getElementById(`location-${item.rowId}`);
                if (select) {
                    select.value = item.locationId;
                }
            }
        }
    });
}


/* =========================================================
   INIT
========================================================= */
document.addEventListener('DOMContentLoaded', async () => {
    console.log('Goods Receipt initialized for PO:', purchaseOrderId);

    await loadPurchaseOrderInfo();
    await loadProducts();
});