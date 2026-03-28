let locationsCache = {};
let receiptItems = [];

function formatNumber(value) {
    return new Intl.NumberFormat('vi-VN').format(value);
}

function showAlert(message, type = 'info', title = null) {
    const modal = new bootstrap.Modal(document.getElementById('alertModal'));
    const header = document.getElementById('alertModalHeader');
    const icon = document.getElementById('alertModalIcon');
    const titleElement = document.getElementById('alertModalTitle');
    const body = document.getElementById('alertModalBody');

    const types = {
        success: { bg: 'bg-success text-white', icon: 'fa-check-circle', title: 'Success' },
        error: { bg: 'bg-danger text-white', icon: 'fa-exclamation-circle', title: 'Error' },
        warning: { bg: 'bg-warning text-white', icon: 'fa-exclamation-triangle', title: 'Warning' },
        info: { bg: 'bg-info text-white', icon: 'fa-info-circle', title: 'Information' }
    };

    const config = types[type] || types.info;
    header.className = `modal-header ${config.bg}`;
    icon.className = `fas ${config.icon} me-2`;
    titleElement.textContent = title || config.title;
    body.innerHTML = message.replace(/\n/g, '<br>');
    modal.show();
}

function showConfirm(message, onConfirm) {
    const modal = new bootstrap.Modal(document.getElementById('confirmModal'));
    const body = document.getElementById('confirmModalBody');
    const okBtn = document.getElementById('confirmModalOkBtn');

    body.innerHTML = message.replace(/\n/g, '<br>');
    const newOkBtn = okBtn.cloneNode(true);
    okBtn.parentNode.replaceChild(newOkBtn, okBtn);
    newOkBtn.addEventListener('click', () => {
        modal.hide();
        if (onConfirm) onConfirm();
    });
    modal.show();
}

async function loadLocationsForProduct(productId, rowId) {
    try {
        if (!locationsCache[productId]) {
            const response = await fetch(`/StockIn/GetAvailableLocations?warehouseId=${warehouseId}&productId=${productId}`);
            locationsCache[productId] = await response.json();
        }

        populateLocationSelect(rowId, locationsCache[productId]);
    } catch (error) {
        console.error('Load locations failed:', error);
    }
}

function populateLocationSelect(rowId, locations) {
    const select = document.getElementById(`location-${rowId}`);
    if (!select) return;

    select.innerHTML = '<option value="">-- Select Location --</option>';

    // Track used locations and their quantities
    const usedLocations = {};
    
    receiptItems.forEach(item => {
        // Collect ALL rows using locations (except current row)
        if (item.locationId && !isNaN(item.locationId) && item.rowId !== rowId) {
            const qtyInput = document.getElementById(`qty-${item.rowId}`);
            const qty = qtyInput ? parseInt(qtyInput.value) || 0 : item.receivedQty;
            
            if (!usedLocations[item.locationId]) {
                usedLocations[item.locationId] = 0;
            }
            usedLocations[item.locationId] += qty;
        }
    });

    locations.forEach(loc => {
        const baseAvailable = loc.maxCapacity - loc.currentStock;
        const usedQty = usedLocations[loc.locationId] || 0;
        const actualAvailable = baseAvailable - usedQty;
        
        // Only show locations with available capacity
        if (actualAvailable <= 0) {
            return;
        }

        const option = document.createElement('option');
        option.value = loc.locationId;
        const totalUsed = loc.currentStock + usedQty;
        option.textContent = `${loc.locationCode} - ${loc.locationName} (${totalUsed}/${loc.maxCapacity})`;
        option.dataset.available = actualAvailable;
        select.appendChild(option);
    });
}

function handleQuantityChange(input) {
    const rowId = input.dataset.rowId;
    const item = receiptItems.find(x => x.rowId === rowId);
    if (item) {
        const newQty = parseInt(input.value) || 0;
        item.receivedQty = newQty;
        
        // Re-check capacity when quantity changes
        const locationSelect = document.getElementById(`location-${rowId}`);
        if (locationSelect && locationSelect.value) {
            checkCapacity(rowId, newQty);
        }
        
        // Refresh ALL location selects to update available capacity display
        refreshAllLocationSelects();
    }
    updateSummary();
}

function refreshAllLocationSelects() {
    receiptItems.forEach(item => {
        const productId = item.productId;
        if (locationsCache[productId]) {
            populateLocationSelect(item.rowId, locationsCache[productId]);
            
            // Re-select the current location if it was set
            if (item.locationId) {
                const select = document.getElementById(`location-${item.rowId}`);
                if (select) {
                    const option = Array.from(select.options).find(opt => parseInt(opt.value) === item.locationId);
                    if (option) {
                        select.value = item.locationId;
                    } else {
                        // Location no longer available, clear it
                        select.value = '';
                        item.locationId = null;
                        clearLocationInfo(item.rowId);
                    }
                }
            }
        }
    });
}

async function handleLocationChange(select) {
    const rowId = select.dataset.rowId;
    const item = receiptItems.find(x => x.rowId === rowId);
    if (item) {
        item.locationId = parseInt(select.value) || null;
        
        // Check capacity for this location
        const qtyInput = document.getElementById(`qty-${rowId}`);
        const quantity = parseInt(qtyInput?.value) || 0;
        if (select.value && quantity > 0) {
            await checkCapacity(rowId, quantity);
        } else {
            clearLocationInfo(rowId);
        }
    }
}

async function checkCapacity(rowId, quantity) {
    const locationSelect = document.getElementById(`location-${rowId}`);
    const locationId = parseInt(locationSelect.value);
    
    if (!locationId || quantity <= 0) {
        clearLocationInfo(rowId);
        return;
    }
    
    try {
        const response = await fetch(`/StockIn/CheckLocationCapacity?locationId=${locationId}`);
        const capacity = await response.json();
        
        const infoDiv = document.getElementById(`location-info-${rowId}`);
        const available = capacity.maxCapacity - capacity.currentStock;
        
        if (quantity <= available) {
            // Enough capacity
            infoDiv.innerHTML = `<span class="text-success"><i class="fas fa-check-circle"></i> Available capacity: ${available} units</span>`;
        } else {
            // Not enough capacity - offer to split
            infoDiv.innerHTML = `
                <span class="text-warning"><i class="fas fa-exclamation-triangle"></i> Only ${available} units available</span>
                <button type="button" class="btn btn-sm btn-warning mt-1" onclick="splitToNewLocation('${rowId}', ${locationSelect.dataset.productId}, ${quantity}, ${available})">
                    <i class="fas fa-plus"></i> Add Another Location
                </button>
            `;
        }
    } catch (error) {
        console.error('Check capacity failed:', error);
    }
}

function clearLocationInfo(rowId) {
    const infoDiv = document.getElementById(`location-info-${rowId}`);
    if (infoDiv) {
        infoDiv.innerHTML = '';
    }
}

function splitToNewLocation(parentRowId, productId, totalQty, firstCapacity) {
    const tbody = document.getElementById('products-tbody');
    const parentRow = document.getElementById(parentRowId);
    
    // Update parent row quantity to first location capacity
    const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
    parentQtyInput.value = firstCapacity;
    parentQtyInput.max = firstCapacity;
    
    const item = receiptItems.find(x => x.rowId === parentRowId);
    if (item) {
        item.receivedQty = firstCapacity;
    }
    
    // Calculate remaining quantity
    const remainingQty = totalQty - firstCapacity;
    
    // Create new row for remaining quantity
    const newRowId = `${parentRowId}-split-${Date.now()}`;
    const productInfo = productsData.find(p => p.productId === productId);
    
    const newRow = document.createElement('tr');
    newRow.id = newRowId;
    newRow.className = 'split-row';
    newRow.innerHTML = `
        <td><div class="sku-badge">${productInfo.sku}</div></td>
        <td>
            <div class="product-name">${productInfo.productName}</div>
            <small class="text-muted">Split from parent row</small>
        </td>
        <td><span class="badge bg-info">${remainingQty}</span></td>
        <td>
            <input type="number"
                   class="form-control qty-input"
                   id="qty-${newRowId}"
                   data-product-id="${productId}"
                   data-row-id="${newRowId}"
                   value="${remainingQty}"
                   min="0"
                   max="${remainingQty}"
                   onchange="handleQuantityChange(this)">
        </td>
        <td>
            <select class="form-select location-select"
                    id="location-${newRowId}"
                    data-product-id="${productId}"
                    data-row-id="${newRowId}"
                    onchange="handleLocationChange(this)">
                <option value="">-- Select Location --</option>
            </select>
            <div class="location-info mt-1" id="location-info-${newRowId}"></div>
            <button type="button" class="btn btn-sm btn-outline-danger mt-1" onclick="removeSplitRow('${newRowId}', '${parentRowId}', ${remainingQty})">
                <i class="fas fa-trash"></i> Remove
            </button>
        </td>
    `;
    
    // Insert new row after parent
    parentRow.after(newRow);
    
    // Add to receiptItems
    receiptItems.push({
        rowId: newRowId,
        productId: productId,
        productName: productInfo.productName,
        receivedQty: remainingQty,
        locationId: null,
        unitPrice: productInfo.unitPrice
    });
    
    // Load locations for new row and refresh all selects
    loadLocationsForProduct(productId, newRowId);
    refreshAllLocationSelects();
    clearLocationInfo(parentRowId);
    updateSummary();
}

function removeSplitRow(rowId, parentRowId, qty) {
    // Remove from DOM
    const row = document.getElementById(rowId);
    if (row) {
        row.remove();
    }
    
    // Remove from receiptItems
    const index = receiptItems.findIndex(x => x.rowId === rowId);
    if (index > -1) {
        receiptItems.splice(index, 1);
    }
    
    // Add quantity back to parent row
    const parentQtyInput = document.getElementById(`qty-${parentRowId}`);
    const currentMax = parseInt(parentQtyInput.max);
    const newMax = currentMax + qty;
    const currentValue = parseInt(parentQtyInput.value);
    const newValue = currentValue + qty;
    
    parentQtyInput.max = newMax;
    parentQtyInput.value = newValue;
    
    const parentItem = receiptItems.find(x => x.rowId === parentRowId);
    if (parentItem) {
        parentItem.receivedQty = newValue;
    }
    
    clearLocationInfo(parentRowId);
    refreshAllLocationSelects();
    
    // Re-check capacity for parent row after merging quantity back
    const parentLocationSelect = document.getElementById(`location-${parentRowId}`);
    if (parentLocationSelect && parentLocationSelect.value) {
        checkCapacity(parentRowId, newValue);
    }
    
    updateSummary();
}

function updateSummary() {
    const totalOrdered = productsData.reduce((sum, p) => sum + p.remainingQty, 0);
    const totalReceived = receiptItems.reduce((sum, item) => sum + item.receivedQty, 0);
    const difference = totalOrdered - totalReceived;

    document.getElementById('total-ordered').textContent = formatNumber(totalOrdered);
    document.getElementById('total-received').textContent = formatNumber(totalReceived);

    const statusElem = document.getElementById('receipt-status');
    if (difference === 0) {
        statusElem.textContent = 'Enough Items';
        statusElem.className = 'text-success';
    } else if (difference > 0) {
        statusElem.textContent = 'Missing items';
        statusElem.className = 'text-warning';
    } else {
        statusElem.textContent = 'Excess items';
        statusElem.className = 'text-danger';
    }
}

async function confirmTransferStockIn() {
    const totalOrdered = productsData.reduce((sum, p) => sum + p.remainingQty, 0);
    const totalReceived = receiptItems.reduce((sum, item) => sum + item.receivedQty, 0);

    // Validate that at least some quantity is being received
    if (totalReceived === 0) {
        showAlert('You must receive at least some quantity.', 'warning', 'Validation Error');
        return;
    }

    // Validate that received quantity doesn't exceed ordered
    if (totalReceived > totalOrdered) {
        showAlert('Received quantity cannot exceed the transfer quantity.', 'warning', 'Validation Error');
        return;
    }

    // Validate locations for items with quantity > 0
    for (const item of receiptItems) {
        if (item.receivedQty > 0 && !item.locationId) {
            showAlert(`Please select a storage location for ${item.productName}.`, 'warning', 'Validation Error');
            return;
        }
    }

    // Determine if this is a partial or full receipt
    const isPartial = totalReceived < totalOrdered;
    const confirmMessage = isPartial
        ? `You are receiving ${totalReceived} out of ${totalOrdered} units.\nThis is a PARTIAL receipt. The transfer will remain active for the remaining quantity.\n\nProceed with partial stock-in?`
        : `You are receiving all ${totalReceived} units.\nThis will COMPLETE the transfer.\n\nProceed with stock-in?`;

    showConfirm(confirmMessage, async () => {
        const requestData = {
            transferId: transferId,
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
            const response = await fetch('/StockIn/ConfirmTransferStockIn', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestData)
            });
            const result = await response.json();

            if (result.success) {
                showAlert(result.message, 'success', 'Stock-In Successful');
                setTimeout(() => {
                    window.location.href = '/StockIn/Index';
                }, 2000);
                return;
            }

            showAlert(result.error, 'error', 'Error');
        } catch (error) {
            console.error('Confirm transfer stock-in failed:', error);
            showAlert('An error occurred during transfer stock-in.', 'error', 'System Error');
        }
    });
}

document.addEventListener('DOMContentLoaded', async () => {
    productsData.forEach((product, index) => {
        const rowId = `product-${product.productId}-${index}`;
        receiptItems.push({
            rowId: rowId,
            productId: product.productId,
            productName: product.productName,
            receivedQty: product.remainingQty,
            locationId: null,
            unitPrice: product.unitPrice
        });
    });

    for (let index = 0; index < productsData.length; index++) {
        const product = productsData[index];
        const rowId = `product-${product.productId}-${index}`;
        await loadLocationsForProduct(product.productId, rowId);
    }

    updateSummary();
});
