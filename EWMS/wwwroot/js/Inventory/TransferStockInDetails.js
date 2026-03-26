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

        const select = document.getElementById(`location-${rowId}`);
        select.innerHTML = '<option value="">-- Select Location --</option>';
        (locationsCache[productId] || []).forEach(loc => {
            const option = document.createElement('option');
            option.value = loc.locationId;
            option.textContent = `${loc.locationCode} - ${loc.locationName} (${loc.currentStock}/${loc.maxCapacity})`;
            option.dataset.available = loc.maxCapacity - loc.currentStock;
            select.appendChild(option);
        });
    } catch (error) {
        console.error('Load locations failed:', error);
    }
}

function handleQuantityChange(input) {
    const item = receiptItems.find(x => x.rowId === input.dataset.rowId);
    if (item) {
        item.receivedQty = parseInt(input.value) || 0;
    }
    updateSummary();
}

function handleLocationChange(select) {
    const item = receiptItems.find(x => x.rowId === select.dataset.rowId);
    if (item) {
        item.locationId = parseInt(select.value) || null;
    }
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

    if (totalReceived !== totalOrdered) {
        showAlert('Received quantity must match the transfer stock-out quantity.', 'warning', 'Validation Error');
        return;
    }

    for (const item of receiptItems) {
        if (item.receivedQty > 0 && !item.locationId) {
            showAlert(`Please select a storage location for ${item.productName}.`, 'warning', 'Validation Error');
            return;
        }
    }

    showConfirm('Are you sure you want to confirm stock-in for this transfer?', async () => {
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
