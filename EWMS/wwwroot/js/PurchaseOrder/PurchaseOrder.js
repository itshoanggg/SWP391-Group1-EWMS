/* =========================================================
   PURCHASE ORDER MODULE - JAVASCRIPT FUNCTIONS
========================================================= */

// Global variables
let productIndex = 1;
let productsData = [];

/* =========================================================
   INDEX PAGE FUNCTIONS
========================================================= */

// Search functionality
function initializeSearch() {
    const searchInput = document.getElementById('searchInput');
    if (searchInput) {
        searchInput.addEventListener('keyup', function () {
            const searchTerm = this.value.toLowerCase();
            const rows = document.querySelectorAll('#purchaseOrderTable tbody tr');

            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                row.style.display = text.includes(searchTerm) ? '' : 'none';
            });
        });
    }
}

// Delete purchase order
async function deletePurchaseOrder(id) {
    if (!confirm('Are you sure you want to delete this order?')) {
        return;
    }

    try {
        const response = await fetch(`/PurchaseOrder/Delete/${id}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            }
        });

        const result = await response.json();

        if (result.success) {
            showAlert('success', result.message);
            setTimeout(() => location.reload(), 1500);
        } else {
            showAlert('error', result.message);
        }
    } catch (error) {
        console.error('Error:', error);
        showAlert('error', 'An error occurred while deleting the order');
    }
}

/* =========================================================
   CREATE PAGE FUNCTIONS
========================================================= */

// Load supplier info and products
async function loadSupplierInfo() {
    const supplierId = document.getElementById('supplierSelect').value;

    if (!supplierId) {
        document.getElementById('supplierInfo').style.display = 'none';
        clearProductSelects();
        return;
    }

    try {
        // Fetch supplier info from API
        const response = await fetch(`/PurchaseOrder/GetSupplierInfo?supplierId=${supplierId}`);
        const data = await response.json();

        if (data.success) {
            document.getElementById('supplierInfo').style.display = 'block';
            document.getElementById('supplierCode').value = 'NCC-' + String(supplierId).padStart(3, '0');
            document.getElementById('supplierPhone').value = data.phone;
        } else {
            document.getElementById('supplierInfo').style.display = 'none';
        }

        // Load products by supplier
        await loadProducts(supplierId);
    } catch (error) {
        console.error('Error loading supplier info:', error);
        alert('Failed to load supplier information');
    }
}

async function loadProducts(supplierId) {
    try {
        const response = await fetch(`/PurchaseOrder/GetProductsBySupplier?supplierId=${supplierId}`);
        productsData = await response.json();

        // Update all product selects
        const selects = document.querySelectorAll('.product-select');
        selects.forEach(select => {
            const currentValue = select.value;
            select.innerHTML = '<option value="">-- Select Product --</option>';

            productsData.forEach(product => {
                const option = document.createElement('option');
                option.value = product.productId;
                option.text = `${product.productName} (${product.categoryName})`;
                option.dataset.sku = 'SKU-' + String(product.productId).padStart(5, '0');
                option.dataset.costPrice = product.costPrice;
                select.appendChild(option);
            });

            if (currentValue) {
                select.value = currentValue;
            }
        });
    } catch (error) {
        console.error('Error loading products:', error);
        alert('Failed to load product list');
    }
}

function updateProduct(select, index) {
    const row = select.closest('.product-row');
    const selectedOption = select.options[select.selectedIndex];

    if (selectedOption.value) {
        // Check for duplicate products
        const selectedProductId = selectedOption.value;
        const allSelects = document.querySelectorAll('.product-select');
        let duplicateCount = 0;
        
        allSelects.forEach(s => {
            if (s.value === selectedProductId) {
                duplicateCount++;
            }
        });

        if (duplicateCount > 1) {
            showAlert('warning', `⚠️ Product "${selectedOption.text}" already selected! Quantities will be merged when creating the order.`);
        }

        const sku = selectedOption.dataset.sku;
        const costPrice = selectedOption.dataset.costPrice;

        row.querySelector('.sku-display').textContent = sku;
        
        // Set suggested price from product (user can edit)
        const priceInput = row.querySelector('.price-input');
        if (!priceInput.value || priceInput.value === '0') {
            // Only set if empty or zero
            priceInput.value = costPrice || '';
        }

        calculateTotal(index);
    } else {
        row.querySelector('.sku-display').textContent = '';
        row.querySelector('.price-input').value = '';
        row.querySelector('.total-display').value = '0';
    }

    updateTotals();
}

function formatPrice(value) {
    // Format number with commas
    if (value) {
        return parseFloat(value).toLocaleString('en-US');
    }
    return '0';
}

function calculateTotal(index) {
    const row = document.querySelector(`.product-row[data-index="${index}"]`);
    const quantity = parseFloat(row.querySelector('.quantity-input').value) || 0;
    
    // Get price directly as number (no formatting needed since input type is now number)
    const price = parseFloat(row.querySelector('.price-input').value) || 0;
    
    const total = quantity * price;

    row.querySelector('.total-display').value = total.toLocaleString('en-US', {minimumFractionDigits: 2, maximumFractionDigits: 2});

    updateTotals();
}

function addProductRow() {
    const supplierId = document.getElementById('supplierSelect').value;

    if (!supplierId) {
        alert('Please select a supplier first');
        return;
    }

    const tbody = document.getElementById('productTableBody');
    const newRow = document.createElement('tr');
    newRow.className = 'product-row';
    newRow.dataset.index = productIndex;

    newRow.innerHTML = `
                <td>
                    <span class="sku-display"></span>
                </td>
                <td>
                    <select name="Details[${productIndex}].ProductId" class="form-select product-select" required onchange="updateProduct(this, ${productIndex})">
                        <option value="">-- Select Product --</option>
                    </select>
                </td>
                <td>
                    <input type="number" name="Details[${productIndex}].Quantity" class="form-control quantity-input"
                           min="1" value="1" required onchange="calculateTotal(${productIndex})">
                </td>
                <td>
                    <input type="number" name="Details[${productIndex}].UnitPrice" class="form-control price-input"
                           min="0" step="0.01" required onchange="calculateTotal(${productIndex})" placeholder="Enter price">
                </td>
                <td>
                    <input type="text" class="form-control total-display" readonly value="0">
                </td>
                <td class="text-center">
                    <button type="button" class="btn btn-sm btn-danger" onclick="removeRow(this)">
                        <i class="fas fa-times"></i>
                    </button>
                </td>
            `;

    tbody.appendChild(newRow);

    // Load products into new select
    const newSelect = newRow.querySelector('.product-select');
    productsData.forEach(product => {
        const option = document.createElement('option');
        option.value = product.productId;
        option.text = `${product.productName} (${product.categoryName})`;
        option.dataset.sku = 'SKU-' + String(product.productId).padStart(5, '0');
        option.dataset.costPrice = product.costPrice;
        newSelect.appendChild(option);
    });

    productIndex++;
    updateTotals();
}

function removeRow(button) {
    button.closest('.product-row').remove();
    updateTotals();
}

function updateTotals() {
    const rows = document.querySelectorAll('.product-row');
    let totalSKU = 0;
    let totalQty = 0;

    rows.forEach(row => {
        const productSelect = row.querySelector('.product-select');
        const quantity = parseFloat(row.querySelector('.quantity-input').value) || 0;

        if (productSelect && productSelect.value) {
            totalSKU++;
            totalQty += quantity;
        }
    });

    const totalSKUElement = document.getElementById('totalSKU');
    const totalQuantityElement = document.getElementById('totalQuantity');

    if (totalSKUElement) totalSKUElement.textContent = totalSKU;
    if (totalQuantityElement) totalQuantityElement.textContent = totalQty;
}

function clearProductSelects() {
    const selects = document.querySelectorAll('.product-select');
    selects.forEach(select => {
        select.innerHTML = '<option value="">-- Select Product --</option>';
    });
    productsData = [];
}

/* =========================================================
   DETAILS PAGE FUNCTIONS
========================================================= */

async function cancelPurchaseOrder(id) {
    if (!confirm('Are you sure you want to cancel this order?')) {
        return;
    }

    try {
        const response = await fetch(`/PurchaseOrder/Cancel/${id}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            }
        });

        const result = await response.json();

        if (result.success) {
            showAlert('success', result.message);
            setTimeout(() => location.reload(), 1500);
        } else {
            showAlert('error', result.message);
        }
    } catch (error) {
        console.error('Error:', error);
        showAlert('error', 'An error occurred while cancelling order');
    }
}

/* =========================================================
   UTILITY FUNCTIONS
========================================================= */

// Show alert message
function showAlert(type, message) {
    const alertTypes = {
        'success': 'alert-success',
        'error': 'alert-danger',
        'warning': 'alert-warning',
        'info': 'alert-info'
    };

    const icons = {
        'success': 'fa-check-circle',
        'error': 'fa-exclamation-circle',
        'warning': 'fa-exclamation-triangle',
        'info': 'fa-info-circle'
    };

    const alertDiv = document.createElement('div');
    alertDiv.className = `alert ${alertTypes[type]} alert-dismissible fade show`;
    alertDiv.setAttribute('role', 'alert');
    alertDiv.innerHTML = `
        <i class="fas ${icons[type]}"></i> ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

    const container = document.querySelector('.container-fluid');
    if (container) {
        container.insertBefore(alertDiv, container.firstChild);

        // Auto dismiss after 5 seconds
        setTimeout(() => {
            alertDiv.remove();
        }, 5000);
    } else {
        alert(message);
    }
}

/* =========================================================
   INITIALIZE ON PAGE LOAD
========================================================= */
document.addEventListener('DOMContentLoaded', function () {
    // Initialize search on Index page
    initializeSearch();

    // Log page loaded
    console.log('Purchase Order module initialized');
});