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

        // DEBUG: console.log removed

        if (data.error) {
            console.error('Error:', data.error);
            return;
        }

        document.getElementById('po-number').textContent = `PO-${String(data.purchaseOrderId).padStart(4, '0')}`;
        document.getElementById('supplier-name').textContent = data.supplierName;
        document.getElementById('supplier-code').textContent = `NCC-${data.supplierId}`;
        document.getElementById('supplier-phone').textContent = data.supplierPhone || 'N/A';

        if (data.hasStockIn) {
            alert('Ðon hàng này dã du?c nh?p kho d?!');
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

        // DEBUG: console.log removed

        if (data.error) {
            console.error('Error:', data.error);
            return;
        }

        productsData = data;

        // ? DEBUG - Ki?m tra t?ng s?n ph?m
        productsData.forEach(p => {
                orderedQty: p.orderedQty,
                receivedQty: p.receivedQty,
                remainingQty: p.remainingQty,
                willShow: p.remainingQty > 0 ? '? SHOW' : '? HIDE'
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

    const tbody = document.getElementById('products-tbody');
    tbody.innerHTML = '';

    let shownCount = 0;
    let hiddenCount = 0;

    productsData.forEach((product, index) => {
        // ?? B? QUA S?N PH?M ÐÃ NH?N Ð?
        if (product.remainingQty <= 0) {
            hiddenCount++;
            return;
        }

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
                    <option value="">-- Ch?n v? trí --</option>
                </select>
                <div class="location-info mt-1" id="location-info-${rowId}"></div>
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

    // DEBUG: console.log removed

    // ? KI?M TRA N?U KHÔNG CÒN S?N PH?M NÀO
    if (receiptItems.length === 0) {
        // DEBUG: console.log removed
        tbody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center py-4">
                    <div class="alert alert-success">
                        <i class="fas fa-check-circle"></i> 
                        <strong>Ðon hàng này dã du?c nh?p kho d?y d?!</strong>
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

function populateLocationSelect(rowId, locations, excludedLocationId = null) { /* ensure options carry available capacity for immediate enforcement */
    const select = document.getElementById(`location-${rowId}`);
    if (!select) return;

    select.innerHTML = '<option value="">-- Ch?n v? trí --</option>';

    // ? Track used locations and their remaining capacity (cho T?T C? products)
    const usedLocations = {};
    
    receiptItems.forEach(item => {
        // ? Ð?m T?T C? các row khác (b?t k? product) dã dùng location
        // Vì capacity KHÔNG phân bi?t product: 200 capacity = 200 s?n ph?m b?t k?
        if (item.locationId && !isNaN(item.locationId) && item.rowId !== rowId) {
            const qtyInput = document.getElementById(`qty-${item.rowId}`);
            const qty = qtyInput ? parseInt(qtyInput.value) || 0 : item.receivedQty;
            
            if (!usedLocations[item.locationId]) {
                usedLocations[item.locationId] = 0;
            }
            usedLocations[item.locationId] += qty;
        }
    });


    const availableOptions = [];
    locations.forEach(loc => {
        const baseAvailable = loc.maxCapacity - loc.currentStock;
        
        // ? Tính t?ng s? lu?ng dã du?c phân b? vào location này (T?T C? các rows, bao g?m c? row hi?n t?i)
        let totalUsedQty = usedLocations[loc.locationId] || 0;
        
        // ? Thêm quantity c?a row HI?N T?I n?u nó cung dang ch?n location này
        const currentItem = receiptItems.find(i => i.rowId === rowId);
        if (currentItem && currentItem.locationId === loc.locationId) {
            const currentQtyInput = document.getElementById(`qty-${rowId}`);
            const currentQty = currentQtyInput ? parseInt(currentQtyInput.value) || 0 : currentItem.receivedQty;
            totalUsedQty += currentQty;
        }
        
        const actualAvailable = baseAvailable - (usedLocations[loc.locationId] || 0);
        
        
        // Only show locations with actual available capacity
        if (actualAvailable <= 0) {
            return;
        }
        if (excludedLocationId && loc.locationId === excludedLocationId) {
            return;
        }

        const option = document.createElement('option');
        option.value = loc.locationId;
        // ? Hi?n th? T?NG s? lu?ng dã dùng (bao g?m c? row hi?n t?i)
        option.textContent = `${loc.locationCode} - ${loc.locationName} (${loc.currentStock + totalUsedQty}/${loc.maxCapacity})`;
        option.dataset.available = actualAvailable;
        select.appendChild(option);
        availableOptions.push(loc.locationCode);
    });

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
    let qty = parseInt(qtyInput.value) || 0;

    const item = receiptItems.find(i => i.rowId === rowId);
    if (!item) {
        return;
    }

    const oldLocationId = item.locationId;
    item.locationId = locationId;

    // If a location is selected, enforce capacity and still show split-suggestion when overflow
    if (locationId) {
        const option = select.options[select.selectedIndex];
        const available = parseInt(option?.dataset?.available || '0');

        // Respect both remaining order qty (existing input max) and rack availability
        const remainingMax = parseInt(qtyInput.max) || qty; // current logical ceiling for this row
        const newMax = Math.min(remainingMax, isNaN(available) ? remainingMax : available);

        qtyInput.max = newMax;

        if (qty > newMax) {
            // 1) Show split suggestion for the overflow amount based on the original qty
            checkCapacity(rowId, qty);
            // 2) Then cap the actual input value to the rack capacity so user can't exceed it
            qty = newMax;
            qtyInput.value = newMax;
            item.receivedQty = newMax;
            // Do NOT call checkCapacity again here to keep the split button visible with the remaining quantity
        } else if (qty > 0) {
            // Within capacity ? just confirm info panel
            checkCapacity(rowId, qty);
        } else {
            clearLocationInfo(rowId);
        }
    } else {
        clearLocationInfo(rowId);
    }



    // ? Refresh T?T C? dropdowns khi location changes
    // Vì capacity KHÔNG phân bi?t product, vi?c ch?n location cho 1 product ?nh hu?ng d?n t?t c?
    if (oldLocationId !== item.locationId) {
        refreshAllLocationSelects();
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

    // ? Tính t?ng s? lu?ng ÐÃ ÐU?C PHÂN B? vào location này t? T?T C? các row khác
    // Capacity KHÔNG phân bi?t product
    let allocatedQty = 0;
    
    receiptItems.forEach(item => {
        if (item.locationId === locationId && item.rowId !== rowId) {
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
                <b>Còn tr?ng sau nh?p: ${available - quantity}</b>
            </div>
        `;
        return;
    }

    const remain = quantity - available;
    infoDiv.innerHTML = `
        <div class="alert alert-warning p-2 mb-2">
            ${locationText}<br>
            <b>Ch? ch?a du?c ${available}</b>
        </div>
        <button class="btn btn-sm btn-warning"
            onclick="splitToNewLocation('${rowId}', ${parseInt(select.dataset.productId)}, ${quantity}, ${available})">
            ? Thêm rack khác cho ${remain}
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

    const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
    parentQtyInput.value = firstCapacity;
    parentQtyInput.max = firstCapacity;
    parentItem.receivedQty = firstCapacity;

    const parentLocationId = parseInt(document.getElementById(`location-${parentRowId}`).value);
    const remainingQty = totalQty - firstCapacity;
    
    
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
                <option value="">-- Ch?n rack khác --</option>
            </select>
            <div id="location-info-${newRowId}" class="mt-1"></div>
        </td>
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
/* Modal selection removed as location is chosen via combobox */
function showLocationModal_removed() {
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
                    <i class="fas fa-check"></i> Ch?n
                </button>
            </td>
        `;
        tbody.appendChild(row);
    });

    const modal = new bootstrap.Modal(document.getElementById('locationModal'));
    modal.show();
}

/* Modal selection removed */
function selectLocationFromModal_removed() {
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

    // DEBUG: console.log removed

    document.getElementById('total-sku').textContent = formatNumber(totalSku);
    document.getElementById('total-ordered').textContent = formatNumber(totalOrdered);
    document.getElementById('total-received').textContent = formatNumber(totalReceived);

    const statusElem = document.getElementById('receipt-status');
    if (difference === 0) {
        statusElem.textContent = 'Ð? hàng';
        statusElem.className = 'text-success';
    } else if (difference > 0) {
        statusElem.textContent = `Thi?u ${formatNumber(difference)} s?n ph?m`;
        statusElem.className = 'text-warning';
    } else {
        statusElem.textContent = `Th?a ${formatNumber(Math.abs(difference))} s?n ph?m`;
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
            errors.push(`${item.productName}: Chua ch?n v? trí luu kho`);
        }
    }

    if (errors.length > 0) {
        alert('Vui lòng ki?m tra:\n\n' + errors.join('\n'));
        return;
    }

    if (!confirm('Xác nh?n nh?p kho các s?n ph?m này?')) {
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

    // DEBUG: console.log removed

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
            alert('L?i: ' + result.error);
        }
    } catch (error) {
        console.error('Confirm stock in failed:', error);
        alert('Có l?i x?y ra khi nh?p kho!');
    }
}

function refreshAllLocationSelects() {

    receiptItems.forEach(item => {
        const locations = locationsCache[item.productId];
        if (!locations) return;

        const select = document.getElementById(`location-${item.rowId}`);
        if (!select) return;

        const currentValue = item.locationId;
        
        // ? Repopulate dropdown v?i updated availability (cho t?t c? products)
        populateLocationSelect(item.rowId, locations);
        
        // Restore previously selected value if still exists
        if (currentValue) {
            const optionExists = Array.from(select.options).some(opt => parseInt(opt.value) === currentValue);
            if (optionExists) {
                select.value = currentValue;
            } else {
                // Location no longer available, clear selection
                item.locationId = null;
            }
        }
    });
}

/* =========================================================
   INIT
========================================================= */
document.addEventListener('DOMContentLoaded', async () => {

    await loadPurchaseOrderInfo();
    await loadProducts();

});

