/* =========================================================
   PURCHASE ORDER MODULE - JAVASCRIPT FUNCTIONS
========================================================= */

// Global variables
let productIndex = 1;
let productsData = [];

/* =========================================================
   INDEX PAGE FUNCTIONS
========================================================= */

// Filter by status
function filterByStatus() {
    const status = document.getElementById('statusFilter').value;
    window.location.href = `/PurchaseOrder/Index?status=${status}`;
}

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
    if (!confirm('Bạn có chắc chắn muốn xóa đơn hàng này?')) {
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
        showAlert('error', 'Có lỗi xảy ra khi xóa đơn hàng');
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

    document.getElementById('supplierInfo').style.display = 'block';
    document.getElementById('supplierCode').value = 'NCC-' + supplierId;

    // Load products by supplier
    await loadProducts(supplierId);
}

async function loadProducts(supplierId) {
    try {
        const response = await fetch(`/PurchaseOrder/GetProductsBySupplier?supplierId=${supplierId}`);
        productsData = await response.json();

        // Update all product selects
        const selects = document.querySelectorAll('.product-select');
        selects.forEach(select => {
            const currentValue = select.value;
            select.innerHTML = '<option value="">-- Chọn sản phẩm --</option>';

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
        alert('Không thể tải danh sách sản phẩm');
    }
}

function updateProduct(select, index) {
    const row = select.closest('.product-row');
    const selectedOption = select.options[select.selectedIndex];

    if (selectedOption.value) {
        const sku = selectedOption.dataset.sku;
        const costPrice = selectedOption.dataset.costPrice;

        row.querySelector('.sku-display').textContent = sku;
        row.querySelector('.price-input').value = costPrice;

        calculateTotal(index);
    } else {
        row.querySelector('.sku-display').textContent = '';
        row.querySelector('.price-input').value = '';
        row.querySelector('.total-display').value = '0';
    }

    updateTotals();
}

function calculateTotal(index) {
    const row = document.querySelector(`.product-row[data-index="${index}"]`);
    const quantity = parseFloat(row.querySelector('.quantity-input').value) || 0;
    const price = parseFloat(row.querySelector('.price-input').value) || 0;
    const total = quantity * price;

    row.querySelector('.total-display').value = total.toLocaleString('vi-VN');

    updateTotals();
}

function addProductRow() {
    const supplierId = document.getElementById('supplierSelect').value;

    if (!supplierId) {
        alert('Vui lòng chọn nhà cung cấp trước');
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
                        <option value="">-- Chọn sản phẩm --</option>
                    </select>
                </td>
                <td>
                    <input type="number" name="Details[${productIndex}].Quantity" class="form-control quantity-input"
                           min="1" value="1" required onchange="calculateTotal(${productIndex})">
                </td>
                <td>
                    <input type="number" name="Details[${productIndex}].UnitPrice" class="form-control price-input"
                           min="0" step="1000" required onchange="calculateTotal(${productIndex})">
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

    // Load products vào select mới
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
        select.innerHTML = '<option value="">-- Chọn sản phẩm --</option>';
    });
    productsData = [];
}

// Form validation before submit
function validateCreateForm(event) {
    const supplierId = document.getElementById('supplierSelect').value;

    if (!supplierId) {
        event.preventDefault();
        showAlert('warning', 'Vui lòng chọn nhà cung cấp');
        return false;
    }

    const rows = document.querySelectorAll('.product-row');
    let hasProduct = false;

    rows.forEach(row => {
        const productId = row.querySelector('.product-select').value;
        if (productId) {
            hasProduct = true;
        }
    });

    if (!hasProduct) {
        event.preventDefault();
        showAlert('warning', 'Vui lòng thêm ít nhất 1 sản phẩm');
        return false;
    }

    return true;
}

/* =========================================================
   DETAILS PAGE FUNCTIONS
========================================================= */

// Mark as delivered (thay thế updateStatus)
async function markAsDelivered(purchaseOrderId) {
    if (!confirm('Xác nhận hàng đã về kho?')) {
        return;
    }

    try {
        const response = await fetch(`/PurchaseOrder/MarkAsDelivered/${purchaseOrderId}`, {
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
        showAlert('error', 'Có lỗi xảy ra khi cập nhật trạng thái');
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

// Format currency
function formatCurrency(value) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND',
        minimumFractionDigits: 0
    }).format(value);
}

// Format number
function formatNumber(value) {
    return new Intl.NumberFormat('vi-VN').format(value);
}

/* =========================================================
   INITIALIZE ON PAGE LOAD
========================================================= */
document.addEventListener('DOMContentLoaded', function () {
    // Initialize search on Index page
    initializeSearch();

    // Initialize form validation on Create page
    const createForm = document.getElementById('createPurchaseOrderForm');
    if (createForm) {
        createForm.addEventListener('submit', validateCreateForm);
    }

    // Log page loaded
    console.log('Purchase Order module initialized');
});

/* =========================================================
   EXPORT FUNCTIONS (if needed for module pattern)
========================================================= */
window.PurchaseOrderModule = {
    filterByStatus,
    deletePurchaseOrder,
    loadSupplierInfo,
    updateProduct,
    calculateTotal,
    addProductRow,
    removeRow,
    markAsDelivered,
    showAlert,
    formatCurrency,
    formatNumber
};