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

        // Only show alert if NOT in read-only mode
        if (data.hasStockIn && !window.readOnlyMode) {
            alert('This purchase order has been fully received!');
            const btnConfirm = document.getElementById('btn-confirm');
            if (btnConfirm) btnConfirm.disabled = true;
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

        // DEBUG - Check products loaded
        console.log('Products loaded:', productsData.length);

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
        // In ReadOnly mode, show all products (including fully received ones)
        // In edit mode, skip products with remainingQty <= 0
        if (!window.readOnlyMode && product.remainingQty <= 0) {
            hiddenCount++;
            return;
        }

        shownCount++;

        const rowId = `product-${product.productId}-${index}`;
        
        // Get allocations for this product (if in ReadOnly mode)
        const allocations = window.readOnlyMode ? (window.allocationsData?.[product.productId] || []) : [];
        
        const row = document.createElement('tr');
        row.id = rowId;
        
        if (window.readOnlyMode) {
            // ReadOnly mode: Display received quantity and locations
            const locationsList = allocations.map(alloc => 
                `<div class="mb-1">
                    <span class="badge bg-info">${alloc.locationCode}</span> 
                    <span class="text-muted">${alloc.locationName}</span>: 
                    <strong>${formatNumber(alloc.quantity)}</strong>
                </div>`
            ).join('');
            
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
                    <span class="badge ${product.receivedQty >= product.orderedQty ? 'bg-success' : 'bg-warning'}">
                        ${formatNumber(product.receivedQty)}
                    </span>
                </td>
                <td>
                    ${locationsList || '<span class="text-muted">No allocations</span>'}
                </td>
            `;
        } else {
            // Edit mode: Show input fields
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
                        <option value="">-- Select Location --</option>
                    </select>
                    <div class="location-info mt-1" id="location-info-${rowId}"></div>
                </td>
            `;
        }

        tbody.appendChild(row);
        
        // Only load locations in edit mode
        if (!window.readOnlyMode) {
            loadLocationsForProduct(product.productId, rowId);
        }

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

    // ? KI?M TRA N?U KH�NG C�N S?N PH?M N�O
    if (receiptItems.length === 0) {
        // DEBUG: console.log removed
        tbody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center py-4">
                    <div class="alert alert-success">
                        <i class="fas fa-check-circle"></i> 
                        <strong>�on h�ng n�y d� du?c nh?p kho d?y d?!</strong>
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

    select.innerHTML = '<option value="">-- Select Location --</option>';

    // Track used locations and their remaining capacity (for ALL products)
    const usedLocations = {};
    
    receiptItems.forEach(item => {
        // ? �?m T?T C? c�c row kh�c (b?t k? product) d� d�ng location
        // V� capacity KH�NG ph�n bi?t product: 200 capacity = 200 s?n ph?m b?t k?
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
        
        // ? T�nh t?ng s? lu?ng d� du?c ph�n b? v�o location n�y (T?T C? c�c rows, bao g?m c? row hi?n t?i)
        let totalUsedQty = usedLocations[loc.locationId] || 0;
        
        // ? Th�m quantity c?a row HI?N T?I n?u n� cung dang Select location n�y
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
        // Display TOTAL used quantity (including current row)
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
    // V� capacity KH�NG ph�n bi?t product, vi?c Select location cho 1 product ?nh hu?ng d?n t?t c?
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
                <b>Number After Stockin: ${available - quantity}</b>
            </div>
        `;
        return;
    }

    const remain = quantity - available;
    infoDiv.innerHTML = `
        <div class="alert alert-warning p-2 mb-2">
            ${locationText}<br>
            <b>Can only hold ${available}</b>
        </div>
        <button class="btn btn-sm btn-warning"
            onclick="splitToNewLocation('${rowId}', ${parseInt(select.dataset.productId)}, ${quantity}, ${available})">
            ? Add to another rack cho ${remain}
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
                <option value="">-- Select rack kh�c --</option>
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
                    <i class="fas fa-check"></i> Select
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
        statusElem.textContent = 'Enough Items';
        statusElem.className = 'text-success';
    } else if (difference > 0) {
        statusElem.textContent = `Missing items`;
        statusElem.className = 'text-warning';
    } else {
        statusElem.textContent = `Excess items`;
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
            errors.push(`${item.productName}: Chua Select v? tr� luu kho`);
        }
    }

    if (errors.length > 0) {
        alert('Please check:\n\n' + errors.join('\n'));
        return;
    }

    if (!confirm('Confirm stock-in for these products?')) {
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
            alert('Error: ' + result.error);
        }
    } catch (error) {
        console.error('Confirm stock in failed:', error);
        alert('An error occurred during stock-in!');
    }
}

function refreshAllLocationSelects() {

    receiptItems.forEach(item => {
        const locations = locationsCache[item.productId];
        if (!locations) return;

        const select = document.getElementById(`location-${item.rowId}`);
        if (!select) return;

        const currentValue = item.locationId;
        
        // Repopulate dropdown with updated availability (for all products)
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
   LOAD ALLOCATIONS (for ReadOnly mode)
========================================================= */
async function loadAllocations() {
    try {
        const response = await fetch(`/StockIn/GetPurchaseOrderAllocations?purchaseOrderId=${purchaseOrderId}`);
        const allocations = await response.json();
        
        if (allocations.error) {
            console.error('Error loading allocations:', allocations.error);
            return {};
        }
        
        // Group allocations by productId
        const allocationsByProduct = {};
        allocations.forEach(alloc => {
            if (!allocationsByProduct[alloc.productId]) {
                allocationsByProduct[alloc.productId] = [];
            }
            allocationsByProduct[alloc.productId].push(alloc);
        });
        
        return allocationsByProduct;
    } catch (error) {
        console.error('Load allocations failed:', error);
        return {};
    }
}

/* =========================================================
   INIT
========================================================= */
document.addEventListener('DOMContentLoaded', async () => {

    await loadPurchaseOrderInfo();
    
    // Load allocations first in ReadOnly mode
    if (window.readOnlyMode) {
        window.allocationsData = await loadAllocations();
    }
    
    await loadProducts();

});

