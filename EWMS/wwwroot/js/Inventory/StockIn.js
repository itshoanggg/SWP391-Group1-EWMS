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
        'ReadyToReceive': { class: 'bg-success', icon: 'fa-check-circle', text: 'Ready To Receive' },
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

        // Check if clickable (ReadyToReceive or PartiallyReceived)
        const isClickable = order.status === 'ReadyToReceive' || order.status === 'PartiallyReceived';
        const isCancelled = order.status === 'Cancelled';

        if (isClickable) {
            row.className = 'clickable';
            row.onclick = () => viewDetails(order.purchaseOrderId);
        } else {
            row.className = 'disabled';
            row.title = isCancelled
                ? 'This order has been cancelled'
                : 'Stock-in is only available for orders with status "Delivered" or "Partially Received"';
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
                ${isClickable
                ? `<button class="btn btn-sm btn-primary" onclick="event.stopPropagation(); viewDetails(${order.purchaseOrderId})">
                            <i class="fas fa-box-open"></i> Stock In
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
});