/* =========================================================
   STOCK IN - INDEX PAGE JAVASCRIPT
========================================================= */

// Pagination state
let currentPage = 1;
let pageSize = 10;
let totalItems = 0;
let allOrders = [];

// Format helpers
function formatCurrency(value) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND',
        minimumFractionDigits: 0
    }).format(value);
}

function formatNumber(value) {
    return new Intl.NumberFormat('vi-VN').format(value);
}

function formatDate(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB');
}

// Get status badge HTML
function getStatusBadge(status) {
    const statusMap = {
        'Ordered': { class: 'bg-info', icon: 'fa-clipboard-list', text: 'Ordered' },
        'PartiallyReceived': { class: 'bg-primary', icon: 'fa-boxes', text: 'Partially Received' },
        'Received': { class: 'bg-dark', icon: 'fa-check-double', text: 'Fully Received' },
        'Cancelled': { class: 'bg-danger', icon: 'fa-ban', text: 'Cancelled' }
    };

    const statusInfo = statusMap[status] || { class: 'bg-secondary', icon: 'fa-question', text: status };
    return `<span class="badge ${statusInfo.class}"><i class="fas ${statusInfo.icon}"></i> ${statusInfo.text}</span>`;
}

// Load purchase orders
async function loadPurchaseOrders() {
    try {
        const status = document.getElementById('filter-status')?.value || '';
        const search = document.getElementById('search-input')?.value || '';
        const tbody = document.querySelector('#purchase-orders-table tbody');

        if (!tbody) {
            console.error('Table tbody not found!');
            return;
        }

        // Show loading
        tbody.innerHTML = `
        <tr>
            <td colspan="8" class="text-center">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
            </td>
        </tr>
        `;

        // Build URL
        const url = `/StockIn/GetPurchaseOrders?warehouseId=${warehouseId}&status=${status}&search=${encodeURIComponent(search)}`;
        console.log('Fetching:', url);

        // Fetch data
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        console.log('Response data:', data);

        // Check for error
        if (data.error) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">
                        <i class="fas fa-exclamation-triangle"></i> ${data.error}
                    </td>
                </tr>
            `;
            return;
        }

        // Check if data is array
        if (!Array.isArray(data)) {
            console.error('Data is not an array:', data);
            tbody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">
                        <i class="fas fa-exclamation-triangle"></i> Invalid data format
                    </td>
                </tr>
            `;
            return;
        }

        // Check if empty
        if (data.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-muted">
                        <i class="fas fa-inbox"></i> No purchase orders found
                    </td>
                </tr>
            `;
            updatePaginationInfo(0, 0, 0);
            renderPagination(0);
            return;
        }

        // Store all orders and calculate pagination
        allOrders = data;
        totalItems = data.length;
        const totalPages = Math.ceil(totalItems / pageSize);
        
        // Reset to page 1 if current page is out of bounds
        if (currentPage > totalPages) {
            currentPage = 1;
        }

        // Get current page data
        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = startIndex + pageSize;
        const pageData = allOrders.slice(startIndex, endIndex);

        // Render data
        renderTableRows(pageData, tbody);

        // Update pagination
        updatePaginationInfo(startIndex + 1, Math.min(endIndex, totalItems), totalItems);
        renderPagination(totalPages);

        console.log(`Loaded ${data.length} purchase orders, showing page ${currentPage} of ${totalPages}`);
    } catch (error) {
        console.error('Load purchase orders failed:', error);

        const tbody = document.querySelector('#purchase-orders-table tbody');
        if (tbody) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">
                        <i class="fas fa-exclamation-triangle"></i> Failed to load data: ${error.message}
                        <br><small>Please open the Console (F12) for more details</small>
                    </td>
                </tr>
            `;
        }
    }
}

// Render table rows
function renderTableRows(orders, tbody) {
    tbody.innerHTML = '';
    orders.forEach(order => {
        const row = document.createElement('tr');

        // All active statuses (Ordered, PartiallyReceived) should open preview modal
        const isActive = order.status === 'Ordered' || order.status === 'PartiallyReceived';
        const isCancelled = order.status === 'Cancelled';

        if (isActive) {
            row.className = 'clickable';
            row.onclick = () => openOrderedPreview(order);
        } else {
            row.className = 'disabled';
            row.title = isCancelled
                ? 'This order has been cancelled'
                : 'Stock-in is only available for active orders';
        }

        // Format expected date
        let expectedDateDisplay = 'N/A';
        if (order.expectedReceivingDate) {
            expectedDateDisplay = formatDate(order.expectedReceivingDate);
        }

        row.innerHTML = `
            <td><strong>PO-${String(order.purchaseOrderId).padStart(4, '0')}</strong></td>
            <td>${order.supplierName || 'N/A'}</td>
            <td>${expectedDateDisplay}</td>
            <td class="text-end">${formatNumber(order.totalItems)}</td>
            <td class="text-end">
                ${order.receivedItems > 0
                ? `<span class="badge bg-success">${formatNumber(order.receivedItems)}</span>`
                : '<span class="text-muted">0</span>'}
            </td>
            <td class="text-end">
                ${isCancelled
                ? '<span class="badge bg-danger">Cancelled</span>'
                : order.remainingItems > 0
                    ? `<span class="badge bg-warning text-dark">${formatNumber(order.remainingItems)}</span>`
                    : '<span class="badge bg-success">Complete</span>'}
            </td>
            <td>${getStatusBadge(order.status)}</td>
            <td>
                ${isActive
                ? `<button class="btn btn-sm btn-primary view-btn" data-order='${JSON.stringify(order)}'>
                            <i class="fas fa-eye"></i> View
                       </button>`
                : isCancelled
                    ? `<button class="btn btn-sm btn-danger" disabled>
                                <i class="fas fa-ban"></i> Cancelled
                           </button>`
                    : `<button class="btn btn-sm btn-secondary" disabled>
                                <i class="fas fa-lock"></i> Not Ready
                           </button>`
            }
            </td>
        `;
        
        // Add event listener to View button if active
        if (isActive) {
            const viewBtn = row.querySelector('.view-btn');
            if (viewBtn) {
                viewBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    openOrderedPreview(order);
                });
            }
        }
        
        tbody.appendChild(row);
    });
}

// Update pagination info
function updatePaginationInfo(start, end, total) {
    const infoElement = document.getElementById('pagination-info');
    if (infoElement) {
        const spanElement = infoElement.querySelector('span:last-child');
        if (spanElement) {
            spanElement.textContent = `Showing ${start} to ${end} of ${total} entries`;
        } else {
            // Fallback if structure is different
            infoElement.innerHTML = `
                <i class="fas fa-info-circle text-primary"></i>
                <span>Showing ${start} to ${end} of ${total} entries</span>
            `;
        }
    }
}

// Render pagination controls
function renderPagination(totalPages) {
    const paginationElement = document.getElementById('pagination-controls');
    if (!paginationElement) {
        return;
    }

    if (totalPages <= 1) {
        paginationElement.innerHTML = '';
        return;
    }

    let html = '<ul class="pagination mb-0">';

    // Previous button
    html += `
        <li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage - 1}); return false;" title="Previous Page">
                <i class="fas fa-chevron-left me-1"></i> Previous
            </a>
        </li>
    `;

    // Page numbers
    const maxVisible = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
    let endPage = Math.min(totalPages, startPage + maxVisible - 1);

    if (endPage - startPage < maxVisible - 1) {
        startPage = Math.max(1, endPage - maxVisible + 1);
    }

    // First page
    if (startPage > 1) {
        html += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(1); return false;" title="Go to page 1">1</a></li>`;
        if (startPage > 2) {
            html += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
        }
    }

    // Page numbers
    for (let i = startPage; i <= endPage; i++) {
        html += `
            <li class="page-item ${i === currentPage ? 'active' : ''}">
                <a class="page-link" href="#" onclick="changePage(${i}); return false;" title="Go to page ${i}">${i}</a>
            </li>
        `;
    }

    // Last page
    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            html += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
        }
        html += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(${totalPages}); return false;" title="Go to page ${totalPages}">${totalPages}</a></li>`;
    }

    // Next button
    html += `
        <li class="page-item ${currentPage === totalPages ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage + 1}); return false;" title="Next Page">
                Next <i class="fas fa-chevron-right ms-1"></i>
            </a>
        </li>
    `;

    html += '</ul>';
    paginationElement.innerHTML = html;
}

// Change page
function changePage(page) {
    const totalPages = Math.ceil(totalItems / pageSize);
    if (page < 1 || page > totalPages) return;
    
    currentPage = page;
    
    // Get current page data
    const startIndex = (currentPage - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    const pageData = allOrders.slice(startIndex, endIndex);
    
    // Render table
    const tbody = document.querySelector('#purchase-orders-table tbody');
    if (tbody) {
        renderTableRows(pageData, tbody);
    }
    
    // Update pagination
    updatePaginationInfo(startIndex + 1, Math.min(endIndex, totalItems), totalItems);
    renderPagination(totalPages);
}

// Change page size
function changePageSize() {
    const pageSizeSelect = document.getElementById('page-size-select');
    if (pageSizeSelect) {
        pageSize = parseInt(pageSizeSelect.value);
        currentPage = 1; // Reset to first page
        
        const totalPages = Math.ceil(totalItems / pageSize);
        const startIndex = 0;
        const endIndex = pageSize;
        const pageData = allOrders.slice(startIndex, endIndex);
        
        // Render table
        const tbody = document.querySelector('#purchase-orders-table tbody');
        if (tbody) {
            renderTableRows(pageData, tbody);
        }
        
        // Update pagination
        updatePaginationInfo(1, Math.min(endIndex, totalItems), totalItems);
        renderPagination(totalPages);
    }
}

// View details (go to stock in form)
function viewDetails(purchaseOrderId) {
    window.location.href = `/StockIn/Details/${purchaseOrderId}`;
}

// Open preview modal for Ordered status
async function openOrderedPreview(order) {
    try {
        const modal = new bootstrap.Modal(document.getElementById('orderedPoModal'));
        
        // Store the purchase order ID
        document.getElementById('ordered-po-id').value = order.purchaseOrderId;
        
        // Reset receipt date and disable proceed link
        const receiptInput = document.getElementById('receipt-date-input');
        const proceedLink = document.getElementById('btn-proceed-stockin');
        if (receiptInput) receiptInput.value = '';
        if (proceedLink) {
            proceedLink.removeAttribute('href');
            proceedLink.classList.add('disabled');
            proceedLink.setAttribute('aria-disabled', 'true');
            proceedLink.setAttribute('tabindex', '-1');
        }
        
        // Show modal
        modal.show();
        
        // Load PO info
        await loadOrderedPoInfo(order.purchaseOrderId);
        
        // Load products
        await loadOrderedPoProducts(order.purchaseOrderId);
        
    } catch (error) {
        console.error('Failed to open ordered preview:', error);
        alert('Failed to load purchase order details');
    }
}

// Load PO info into modal
async function loadOrderedPoInfo(purchaseOrderId) {
    try {
        const response = await fetch(`/StockIn/GetPurchaseOrderInfo?purchaseOrderId=${purchaseOrderId}`);
        const data = await response.json();
        
        if (data.error) {
            throw new Error(data.error);
        }
        
        const infoDiv = document.getElementById('ordered-po-info');
        infoDiv.innerHTML = `
            <div class="row g-3">
                <div class="col-md-6">
                    <p class="mb-1"><strong>Order Number:</strong> <span class="badge bg-info">PO-${String(data.purchaseOrderId).padStart(4, '0')}</span></p>
                    <p class="mb-1"><strong>Supplier:</strong> ${data.supplierName || 'N/A'}</p>
                </div>
                <div class="col-md-6">
                    <p class="mb-1"><strong>Expected Date:</strong> ${formatDate(data.expectedReceivingDate)}</p>
                    <p class="mb-1"><strong>Status:</strong> ${getStatusBadge(data.status)}</p>
                </div>
            </div>
        `;
    } catch (error) {
        console.error('Failed to load PO info:', error);
        throw error;
    }
}

// Load products into modal table
async function loadOrderedPoProducts(purchaseOrderId) {
    try {
        const response = await fetch(`/StockIn/GetPurchaseOrderProducts?purchaseOrderId=${purchaseOrderId}`);
        const products = await response.json();
        
        if (products.error) {
            throw new Error(products.error);
        }
        
        const tbody = document.querySelector('#ordered-po-products tbody');
        const totalElement = document.getElementById('ordered-po-total');
        
        if (!Array.isArray(products) || products.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-muted py-3">No products found</td>
                </tr>
            `;
            totalElement.textContent = formatCurrency(0);
            return;
        }
        
        let total = 0;
        tbody.innerHTML = '';
        
        products.forEach(product => {
            const lineTotal = (product.quantity || 0) * (product.unitPrice || 0);
            total += lineTotal;
            
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${product.sku || 'N/A'}</td>
                <td>
                    <div><strong>${product.productName || 'N/A'}</strong></div>
                    <small class="text-muted">${product.categoryName || ''}</small>
                </td>
                <td class="text-end">${formatNumber(product.quantity || 0)}</td>
                <td class="text-end">${formatCurrency(product.unitPrice || 0)}</td>
                <td class="text-end">${formatCurrency(lineTotal)}</td>
            `;
            tbody.appendChild(row);
        });
        
        totalElement.textContent = formatCurrency(total);
        
    } catch (error) {
        console.error('Failed to load products:', error);
        const tbody = document.querySelector('#ordered-po-products tbody');
        tbody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center text-danger py-3">
                    <i class="fas fa-exclamation-triangle"></i> Failed to load products
                </td>
            </tr>
        `;
        throw error;
    }
}

// Check if receipt date is valid
function checkReceiptDate() {
    const receiptDateInput = document.getElementById('receipt-date-input');
    const proceedLink = document.getElementById('btn-proceed-stockin');
    const hintElement = document.getElementById('receipt-date-hint');
    const poId = document.getElementById('ordered-po-id')?.value;

    const disableLink = () => {
        if (proceedLink) {
            proceedLink.removeAttribute('href');
            proceedLink.classList.add('disabled');
            proceedLink.setAttribute('aria-disabled', 'true');
            proceedLink.setAttribute('tabindex', '-1');
        }
    };

    if (!receiptDateInput.value) {
        disableLink();
        hintElement.textContent = 'Please select a receipt date to proceed.';
        hintElement.className = 'form-text text-muted';
        return;
    }

    const selectedDate = new Date(receiptDateInput.value);
    const today = new Date();

    // Reset time parts for comparison
    selectedDate.setHours(0, 0, 0, 0);
    today.setHours(0, 0, 0, 0);

    // Allow today or past dates (not future dates)
    if (selectedDate.getTime() <= today.getTime() && poId) {
        if (proceedLink) {
            proceedLink.href = `/StockIn/Details/${poId}`;
            proceedLink.classList.remove('disabled');
            proceedLink.removeAttribute('aria-disabled');
            proceedLink.removeAttribute('tabindex');
        }
        hintElement.textContent = 'Ready to proceed to Stock-In!';
        hintElement.className = 'form-text text-success';
    } else {
        disableLink();
        hintElement.textContent = 'Receipt date cannot be in the future.';
        hintElement.className = 'form-text text-danger';
    }
}

// Proceed link now handled via dynamic href on #btn-proceed-stockin

// Search handler with debounce
let searchTimeout;
function handleSearch() {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        currentPage = 1; // Reset to first page on search
        loadPurchaseOrders();
    }, 500);
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    if (!warehouseId) {
        alert('Error: WarehouseId not found!');
        console.error('warehouseId is undefined');
        return;
    }

    console.log('Stock In page initialized for warehouse:', warehouseId);
    loadPurchaseOrders();

    // Add event listeners
    const filterStatus = document.getElementById('filter-status');
    const searchInput = document.getElementById('search-input');

    if (filterStatus) {
        filterStatus.addEventListener('change', loadPurchaseOrders);
    }

    if (searchInput) {
        searchInput.addEventListener('keyup', handleSearch);
    }
    
    // Add event listener for receipt date
    const receiptDateInput = document.getElementById('receipt-date-input');
    if (receiptDateInput) {
        receiptDateInput.addEventListener('change', checkReceiptDate);
    }
    
    // Proceed is handled by anchor href; no click handler needed
});