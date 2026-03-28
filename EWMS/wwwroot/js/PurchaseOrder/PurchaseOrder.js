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

/* =========================================================
   CREATE PAGE FUNCTIONS
========================================================= */

// Load supplier info and products
async function loadSupplierInfo() {
    const supplierId = document.getElementById('supplierSelect').value;

    if (!supplierId) {
        document.getElementById('supplierInfo').style.display = 'none';
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

        // DO NOT filter products by supplier - products are already loaded and should stay the same
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

async function updateProduct(select, index) {
    const row = select.closest('.product-row');
    const selectedOption = select.options[select.selectedIndex];

    console.log('updateProduct called, selectedOption:', selectedOption);

    if (selectedOption.value) {
        // Check for duplicate products
        const selectedProductId = selectedOption.value;
        const allSelects = document.querySelectorAll('.product-select');
        let isDuplicate = false;
        
        allSelects.forEach(s => {
            // Check if another select (not this one) has the same product
            if (s !== select && s.value === selectedProductId) {
                isDuplicate = true;
            }
        });

        if (isDuplicate) {
            showAlert('error', `❌ Product "${selectedOption.text}" has already been selected! Please choose a different product.`);
            // Reset the selection
            select.value = '';
            row.querySelector('.sku-display').textContent = '';
            row.querySelector('.price-value').value = '';
            row.querySelector('.price-display').value = '';
            row.querySelector('.total-display').value = '0';
            updateTotals();
            return; // Stop processing
        }

        const sku = selectedOption.dataset.sku;
        const costPrice = selectedOption.dataset.costPrice;

        console.log('SKU:', sku, 'CostPrice:', costPrice);

        row.querySelector('.sku-display').textContent = sku || '';
        
        // Set suggested price from product (always update when product changes)
        const priceDisplay = row.querySelector('.price-display');
        const priceValue = row.querySelector('.price-value');
        const costPriceNum = parseInt(costPrice) || 0;
        priceValue.value = costPriceNum;
        priceDisplay.value = costPriceNum > 0 ? costPriceNum.toLocaleString('en-US') : '';

        calculateTotal(index);

        console.log('Calling filterSuppliersByProduct with productId:', selectedProductId);
        // Filter suppliers based on selected product - this will reset supplier selection
        await filterSuppliersByProduct(selectedProductId);
    } else {
        row.querySelector('.sku-display').textContent = '';
        row.querySelector('.price-value').value = '';
        row.querySelector('.price-display').value = '';
        row.querySelector('.total-display').value = '0';
    }

    updateTotals();
}

async function filterSuppliersByProduct(productId) {
    try {
        const response = await fetch(`/PurchaseOrder/GetSuppliersByProduct?productId=${productId}`);
        const data = await response.json();

        console.log('API Response for productId', productId, ':', data);

        const supplierSelect = document.getElementById('supplierSelect');

        if (data.success && data.suppliers && data.suppliers.length > 0) {
            console.log('Found', data.suppliers.length, 'suppliers, enabling dropdown');
            // Enable and rebuild supplier dropdown - ALWAYS RESET when product changes
            supplierSelect.disabled = false;
            supplierSelect.innerHTML = '<option value="">-- Select Supplier --</option>';
            
            data.suppliers.forEach(supplier => {
                const option = document.createElement('option');
                option.value = supplier.supplierId;
                option.text = supplier.supplierName;
                supplierSelect.appendChild(option);
            });

            // Hide supplier info since we reset the selection
            document.getElementById('supplierInfo').style.display = 'none';
        } else {
            console.log('No suppliers found or API failed, disabling dropdown');
            // Disable supplier select and show warning
            supplierSelect.disabled = true;
            supplierSelect.innerHTML = '<option value="">-- No Suppliers Available --</option>';
            document.getElementById('supplierInfo').style.display = 'none';
            showAlert('warning', '⚠️ No suppliers found for this product. Please select a different product.');
        }
    } catch (error) {
        console.error('Error filtering suppliers:', error);
        const supplierSelect = document.getElementById('supplierSelect');
        supplierSelect.disabled = true;
        supplierSelect.innerHTML = '<option value="">-- Error Loading Suppliers --</option>';
        document.getElementById('supplierInfo').style.display = 'none';
    }
}

function formatPrice(value) {
    // Format number with commas
    if (value) {
        return parseFloat(value).toLocaleString('en-US');
    }
    return '0';
}

function handlePriceInput(input, index) {
    // Remove any non-digit characters except decimal point
    let value = input.value.replace(/[^\d.]/g, '');
    
    // Get the numeric value
    const numericValue = parseFloat(value) || 0;
    
    // Update the hidden field with raw number
    const row = input.closest('.product-row');
    row.querySelector('.price-value').value = numericValue;
    
    // Format and display with commas
    input.value = numericValue > 0 ? numericValue.toLocaleString('en-US') : '';
    
    calculateTotal(index);
}

function calculateTotal(index) {
    const row = document.querySelector(`.product-row[data-index="${index}"]`);
    const quantity = parseFloat(row.querySelector('.quantity-input').value) || 0;
    
    // Get price from hidden field
    const price = parseFloat(row.querySelector('.price-value').value) || 0;
    
    const total = quantity * price;

    row.querySelector('.total-display').value = total.toLocaleString('en-US');

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
                    <input type="hidden" name="Details[${productIndex}].UnitPrice" class="price-value">
                    <input type="text" class="form-control price-display"
                           required oninput="handlePriceInput(this, ${productIndex})" placeholder="Enter price">
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

    // Load products into new select from the first row's options
    const firstSelect = document.querySelector('.product-select');
    const newSelect = newRow.querySelector('.product-select');
    
    if (firstSelect) {
        // Copy all options from the first select (which has all products from ViewBag)
        Array.from(firstSelect.options).forEach(option => {
            const newOption = option.cloneNode(true);
            newSelect.appendChild(newOption);
        });
    }

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