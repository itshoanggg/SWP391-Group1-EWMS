/* =========================================================
   GOODS RECEIPT - MAIN LOGIC (WITH DEBUG)
========================================================= */

let productsData = [];
let locationsCache = {};
let receiptItems = [];
let locationUsage = {};

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

        console.log('📋 PO Info:', data); // ✅ DEBUG

        if (data.error) {
            console.error('Error:', data.error);
            return;
        }

        document.getElementById('po-number').textContent = `PO-${String(data.purchaseOrderId).padStart(4, '0')}`;
        document.getElementById('supplier-name').textContent = data.supplierName;
        document.getElementById('supplier-code').textContent = `NCC-${data.supplierId}`;
        document.getElementById('supplier-phone').textContent = data.supplierPhone || 'N/A';

        if (data.hasStockIn) {
            alert('Đơn hàng này đã được nhập kho đủ!');
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

        console.log('📦 Products Data from API:', data); // ✅ DEBUG

        if (data.error) {
            console.error('Error:', data.error);
            return;
        }

        productsData = data;

        // ✅ DEBUG - Kiểm tra từng sản phẩm
        console.log('🔍 Checking each product:');
        productsData.forEach(p => {
            console.log(`  ${p.productName}:`, {
                orderedQty: p.orderedQty,
                receivedQty: p.receivedQty,
                remainingQty: p.remainingQty,
                willShow: p.remainingQty > 0 ? '✅ SHOW' : '❌ HIDE'
            });
        });

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
    console.log('🎨 Starting renderProductsTable()...'); // ✅ DEBUG

    const tbody = document.getElementById('products-tbody');
    tbody.innerHTML = '';

    let shownCount = 0;
    let hiddenCount = 0;

    productsData.forEach((product, index) => {
        // ⚠️ BỎ QUA SẢN PHẨM ĐÃ NHẬN ĐỦ
        if (product.remainingQty <= 0) {
            console.log(`  ⏭️ Skipping ${product.productName} (remainingQty = ${product.remainingQty})`); // ✅ DEBUG
            hiddenCount++;
            return;
        }

        console.log(`  ✅ Showing ${product.productName} (remainingQty = ${product.remainingQty})`); // ✅ DEBUG
        shownCount++;

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
                       value="${product.remainingQty}"
                       min="0"
                       max="${product.remainingQty}"
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
        loadLocationsForProduct(product.productId, rowId);

        receiptItems.push({
            rowId: rowId,
            productId: product.productId,
            productName: product.productName,
            orderedQty: product.orderedQty,
            receivedQty: product.remainingQty,
            locationId: null,
            unitPrice: product.unitPrice,
            splitRows: []
        });
    });

    console.log(`📊 Render summary: ${shownCount} shown, ${hiddenCount} hidden`); // ✅ DEBUG

    // ✅ KIỂM TRA NẾU KHÔNG CÒN SẢN PHẨM NÀO
    if (receiptItems.length === 0) {
        console.log('🎉 All products received!'); // ✅ DEBUG
        tbody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center py-4">
                    <div class="alert alert-success">
                        <i class="fas fa-check-circle"></i> 
                        <strong>Đơn hàng này đã được nhập kho đầy đủ!</strong>
                    </div>
                </td>
            </tr>
        `;
        document.getElementById('btn-confirm').disabled = true;
    }
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

    // Get the product ID for this row to check usage across all rows of the same product
    const productId = parseInt(select.dataset.productId);
    
    // Track used locations and their remaining capacity for this product
    const usedLocations = {};
    
    receiptItems.forEach(item => {
        // Only count items with valid locationId (not null, not undefined, not NaN)
        if (item.productId === productId && item.locationId && !isNaN(item.locationId) && item.rowId !== rowId) {
            const qtyInput = document.getElementById(`qty-${item.rowId}`);
            const qty = qtyInput ? parseInt(qtyInput.value) || 0 : item.receivedQty;
            
            if (!usedLocations[item.locationId]) {
                usedLocations[item.locationId] = 0;
            }
            usedLocations[item.locationId] += qty;
        }
    });

    console.log(`📍 Populating locations for rowId: ${rowId}, productId: ${productId}`);
    console.log(`   Used locations:`, usedLocations);
    console.log(`   All receiptItems for this product:`, receiptItems.filter(i => i.productId === productId).map(i => ({
        rowId: i.rowId,
        locationId: i.locationId,
        receivedQty: i.receivedQty
    })));

    const availableOptions = [];
    locations.forEach(loc => {
        const baseAvailable = loc.maxCapacity - loc.currentStock;
        
        // Subtract quantity already allocated to this location by other rows
        const usedQty = usedLocations[loc.locationId] || 0;
        const actualAvailable = baseAvailable - usedQty;
        
        console.log(`   ${loc.locationCode}: baseAvailable=${baseAvailable}, usedQty=${usedQty}, actualAvailable=${actualAvailable}`);
        
        // Only show locations with actual available capacity
        if (actualAvailable <= 0) {
            console.log(`   ❌ ${loc.locationCode} excluded (actualAvailable <= 0)`);
            return;
        }
        if (excludedLocationId && loc.locationId === excludedLocationId) {
            console.log(`   ❌ ${loc.locationCode} excluded (explicitly excluded)`);
            return;
        }

        const option = document.createElement('option');
        option.value = loc.locationId;
        option.textContent = `${loc.locationCode} - ${loc.locationName} (${loc.currentStock + usedQty}/${loc.maxCapacity})`;
        option.dataset.available = actualAvailable;
        select.appendChild(option);
        availableOptions.push(loc.locationCode);
    });

    console.log(`   ✅ Available options: [${availableOptions.join(', ')}]`);
}

/* =========================================================
   HANDLE QUANTITY CHANGE
========================================================= */
function handleQuantityChange(input) {
    const rowId = input.dataset.rowId;
    const productId = parseInt(input.dataset.productId);
    const newQty = parseInt(input.value) || 0;

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
    const locationId = parseInt(select.value) || null;
    const qtyInput = document.getElementById(`qty-${rowId}`);
    const qty = parseInt(qtyInput.value) || 0;

    const item = receiptItems.find(i => i.rowId === rowId);
    if (!item) {
        console.log(`⚠️ Item not found for rowId: ${rowId}`);
        return;
    }

    const oldLocationId = item.locationId;
    item.locationId = locationId;

    console.log(`🔄 Location changed for ${rowId}: ${oldLocationId} → ${item.locationId}, qty: ${qty}`);

    if (locationId && qty > 0) {
        checkCapacity(rowId, qty);
    } else {
        clearLocationInfo(rowId);
    }

    // Refresh all dropdowns for the same product when location changes
    // This ensures all dropdowns reflect the latest allocations
    if (oldLocationId !== item.locationId) {
        console.log(`🔄 Triggering refresh for all location selects (productId: ${item.productId})`);
        refreshAllLocationSelects(item.productId);
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

    // Calculate already allocated quantity to this location from other rows
    const productId = parseInt(select.dataset.productId);
    let allocatedQty = 0;
    
    receiptItems.forEach(item => {
        if (item.productId === productId && item.locationId === locationId && item.rowId !== rowId) {
            const qtyInput = document.getElementById(`qty-${item.rowId}`);
            allocatedQty += qtyInput ? parseInt(qtyInput.value) || 0 : item.receivedQty;
        }
    });

    const baseAvailable = data.maxCapacity - data.currentStock;
    const available = baseAvailable - allocatedQty;
    const locationText = select.options[select.selectedIndex].text;

    if (quantity <= available) {
        infoDiv.innerHTML = `
            <div class="alert alert-success p-2 mb-0">
                ${locationText}<br>
                <b>Còn trống sau nhập: ${available - quantity}</b>
            </div>
        `;
        return;
    }

    const remain = quantity - available;
    infoDiv.innerHTML = `
        <div class="alert alert-warning p-2 mb-2">
            ${locationText}<br>
            <b>Chỉ chứa được ${available}</b>
        </div>
        <button class="btn btn-sm btn-warning"
            onclick="splitToNewLocation('${rowId}', ${parseInt(select.dataset.productId)}, ${quantity}, ${available})">
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
    console.log(`➕ Splitting location: parentRow=${parentRowId}, totalQty=${totalQty}, firstCapacity=${firstCapacity}`);
    
    const parentRow = document.getElementById(parentRowId);
    const parentItem = receiptItems.find(i => i.rowId === parentRowId);
    const product = productsData.find(p => p.productId === productId);

    if (!parentRow || !parentItem || !product) return;

    const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
    parentQtyInput.value = firstCapacity;
    parentQtyInput.max = firstCapacity;
    parentItem.receivedQty = firstCapacity;

    const parentLocationId = parseInt(document.getElementById(`location-${parentRowId}`).value);
    const remainingQty = totalQty - firstCapacity;
    
    console.log(`   Parent location: ${parentLocationId}, remaining qty: ${remainingQty}`);
    
    if (remainingQty <= 0) return;

    const newRowId = `split-${Date.now()}`;
    const newRow = document.createElement('tr');
    newRow.id = newRowId;
    newRow.className = 'split-row bg-light';

    newRow.innerHTML = `
        <td><span class="sku-badge bg-warning">${product.sku}</span></td>
        <td>${product.productName}</td>
        <td><span class="badge bg-warning">${remainingQty}</span></td>
        <td>
            <input type="number" id="qty-${newRowId}" class="form-control"
                   value="${remainingQty}" min="1" max="${remainingQty}"
                   data-row-id="${newRowId}" data-product-id="${productId}"
                   onchange="handleQuantityChange(this)">
        </td>
        <td>
            <select id="location-${newRowId}" class="form-select"
                    data-row-id="${newRowId}" data-product-id="${productId}"
                    onchange="handleLocationChange(this)">
                <option value="">-- Chọn rack khác --</option>
            </select>
            <div id="location-info-${newRowId}" class="mt-1"></div>
        </td>
        <td></td>
    `;

    parentRow.after(newRow);

    const newItem = {
        rowId: newRowId,
        productId,
        productName: product.productName,
        orderedQty: remainingQty,
        receivedQty: remainingQty,
        locationId: null,
        unitPrice: product.unitPrice,
        isSplit: true,
        parentRowId
    };
    
    receiptItems.push(newItem);
    
    console.log(`   Created new split row: ${newRowId}`);
    console.log(`   Current receiptItems for product ${productId}:`, receiptItems.filter(i => i.productId === productId));

    // Populate dropdown - it will automatically exclude used-up locations
    populateLocationSelect(newRowId, locationsCache[productId]);
    updateSummary();
}

function removeSplitRow(rowId, parentRowId, qty) {
    const row = document.getElementById(rowId);
    if (row) row.remove();

    const index = receiptItems.findIndex(i => i.rowId === rowId);
    if (index > -1) receiptItems.splice(index, 1);

    const parentItem = receiptItems.find(i => i.rowId === parentRowId);
    if (parentItem) {
        const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
        const currentMax = parseInt(parentQtyInput.max);
        parentQtyInput.max = currentMax + qty;

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
    const totalSku = productsData.filter(p => p.remainingQty > 0).length;
    const totalOrdered = productsData.reduce((sum, p) => sum + p.remainingQty, 0);
    const totalReceived = receiptItems.reduce((sum, item) => sum + item.receivedQty, 0);
    const difference = totalOrdered - totalReceived;

    console.log('📊 Summary:', { totalSku, totalOrdered, totalReceived, difference }); // ✅ DEBUG

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

    console.log('💾 Submitting:', requestData); // ✅ DEBUG

    try {
        const response = await fetch('/StockIn/ConfirmStockIn', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
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

    console.log('🔄 Refreshing all location selects for productId:', productId);

    receiptItems.forEach(item => {
        if (item.productId === productId) {
            const select = document.getElementById(`location-${item.rowId}`);
            if (!select) return;

            const currentValue = item.locationId;
            
            // Repopulate the dropdown with updated availability
            populateLocationSelect(item.rowId, locations);
            
            // Restore the previously selected value if it still exists in the dropdown
            if (currentValue) {
                const optionExists = Array.from(select.options).some(opt => parseInt(opt.value) === currentValue);
                if (optionExists) {
                    select.value = currentValue;
                } else {
                    // Location is no longer available, clear the selection
                    console.log(`⚠️ Location ${currentValue} no longer available for row ${item.rowId}`);
                    item.locationId = null;
                }
            }
        }
    });
}

/* =========================================================
   INIT
========================================================= */
document.addEventListener('DOMContentLoaded', async () => {
    console.log('🚀 Goods Receipt initialized');
    console.log('   PO ID:', purchaseOrderId);
    console.log('   Warehouse ID:', warehouseId);
    console.log('   User ID:', userId);

    await loadPurchaseOrderInfo();
    await loadProducts();

    console.log('✅ Initialization complete');
    console.log('📋 Final receiptItems:', receiptItems);
});