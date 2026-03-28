
let currentPage = 1;
let pageSize = 10;
let totalItems = 0;
let allOrders = [];

let currentPoInfo = null;


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


function toInputDate(date) {
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const dd = String(date.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
}


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


async function loadPurchaseOrders() {
    try {
        const status = document.getElementById('filter-status')?.value || '';
        const search = document.getElementById('search-input')?.value || '';
        const tbody = document.querySelector('#purchase-orders-table tbody');

        if (!tbody) {
            console.error('Table tbody not found!');
            return;
        }


        tbody.innerHTML = `
        <tr>
            <td colspan="8" class="text-center">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
            </td>
        </tr>
        `;


        const url = `/StockIn/GetPurchaseOrders?warehouseId=${warehouseId}&status=${status}&search=${encodeURIComponent(search)}`;
        console.log('Fetching:', url);

 
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        console.log('Response data:', data);


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


        allOrders = data;
        totalItems = data.length;
        const totalPages = Math.ceil(totalItems / pageSize);
        

        if (currentPage > totalPages) {
            currentPage = 1;
        }


        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = startIndex + pageSize;
        const pageData = allOrders.slice(startIndex, endIndex);


        renderTableRows(pageData, tbody);

 
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


function renderTableRows(orders, tbody) {
    tbody.innerHTML = '';
    orders.forEach(order => {
        const row = document.createElement('tr');


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


function updatePaginationInfo(start, end, total) {
    const infoElement = document.getElementById('pagination-info');
    if (infoElement) {
        const spanElement = infoElement.querySelector('span:last-child');
        if (spanElement) {
            spanElement.textContent = `Showing ${start} to ${end} of ${total} entries`;
        } else {

            infoElement.innerHTML = `
                <i class="fas fa-info-circle text-primary"></i>
                <span>Showing ${start} to ${end} of ${total} entries</span>
            `;
        }
    }
}


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


    html += `
        <li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage - 1}); return false;" title="Previous Page">
                <i class="fas fa-chevron-left me-1"></i> Previous
            </a>
        </li>
    `;

    
    const maxVisible = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
    let endPage = Math.min(totalPages, startPage + maxVisible - 1);

    if (endPage - startPage < maxVisible - 1) {
        startPage = Math.max(1, endPage - maxVisible + 1);
    }


    if (startPage > 1) {
        html += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(1); return false;" title="Go to page 1">1</a></li>`;
        if (startPage > 2) {
            html += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
        }
    }


    for (let i = startPage; i <= endPage; i++) {
        html += `
            <li class="page-item ${i === currentPage ? 'active' : ''}">
                <a class="page-link" href="#" onclick="changePage(${i}); return false;" title="Go to page ${i}">${i}</a>
            </li>
        `;
    }


    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            html += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
        }
        html += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(${totalPages}); return false;" title="Go to page ${totalPages}">${totalPages}</a></li>`;
    }


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


function changePage(page) {
    const totalPages = Math.ceil(totalItems / pageSize);
    if (page < 1 || page > totalPages) return;
    
    currentPage = page;
    

    const startIndex = (currentPage - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    const pageData = allOrders.slice(startIndex, endIndex);
    

    const tbody = document.querySelector('#purchase-orders-table tbody');
    if (tbody) {
        renderTableRows(pageData, tbody);
    }
    

    updatePaginationInfo(startIndex + 1, Math.min(endIndex, totalItems), totalItems);
    renderPagination(totalPages);
}


function changePageSize() {
    const pageSizeSelect = document.getElementById('page-size-select');
    if (pageSizeSelect) {
        pageSize = parseInt(pageSizeSelect.value);
        currentPage = 1; 
        
        const totalPages = Math.ceil(totalItems / pageSize);
        const startIndex = 0;
        const endIndex = pageSize;
        const pageData = allOrders.slice(startIndex, endIndex);
        

        const tbody = document.querySelector('#purchase-orders-table tbody');
        if (tbody) {
            renderTableRows(pageData, tbody);
        }
        

        updatePaginationInfo(1, Math.min(endIndex, totalItems), totalItems);
        renderPagination(totalPages);
    }
}


function viewDetails(purchaseOrderId) {
    window.location.href = `/StockIn/Details/${purchaseOrderId}`;
}


async function openOrderedPreview(order) {
    try {
        const modal = new bootstrap.Modal(document.getElementById('orderedPoModal'));
        

        document.getElementById('ordered-po-id').value = order.purchaseOrderId;
        
 
        currentPoInfo = null;
        const proceedLink = document.getElementById('btn-proceed-stockin');
        

        if (proceedLink) {
            proceedLink.href = `/StockIn/Details/${order.purchaseOrderId}`;
            proceedLink.classList.remove('disabled');
            proceedLink.removeAttribute('aria-disabled');
            proceedLink.removeAttribute('tabindex');
        }
        

        modal.show();
        

        await loadOrderedPoInfo(order.purchaseOrderId);
        

        await loadOrderedPoProducts(order.purchaseOrderId);
        
    } catch (error) {
        console.error('Failed to open ordered preview:', error);
        alert('Failed to load purchase order details');
    }
}


async function loadOrderedPoInfo(purchaseOrderId) {
    try {
        const response = await fetch(`/StockIn/GetPurchaseOrderInfo?purchaseOrderId=${purchaseOrderId}`);
        const data = await response.json();
        
        if (data.error) {
            throw new Error(data.error);
        }
        
        currentPoInfo = data;
        
        const infoDiv = document.getElementById('ordered-po-info');
        infoDiv.innerHTML = `
            <div class="row g-3">
                <div class="col-md-6">
                    <p class="mb-1"><strong>Order Number:</strong> <span class="badge bg-info">PO-${String(data.purchaseOrderId).padStart(4, '0')}</span></p>
                    <p class="mb-1"><strong>Supplier:</strong> ${data.supplierName || 'N/A'}</p>
                </div>
                <div class="col-md-6">
                    <p class="mb-1"><strong>PO Created:</strong> ${formatDate(data.createdAt)}</p>
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

function checkReceiptDate() {
    const receiptDateInput = document.getElementById('receipt-date-input');
    const proceedLink = document.getElementById('btn-proceed-stockin');
    const hintElement = document.getElementById('receipt-date-hint');
    const poId = document.getElementById('ordered-po-id')?.value;

    const disableLink = (message, className = 'form-text text-danger') => {
        if (proceedLink) {
            proceedLink.removeAttribute('href');
            proceedLink.classList.add('disabled');
            proceedLink.setAttribute('aria-disabled', 'true');
            proceedLink.setAttribute('tabindex', '-1');
        }
        if (hintElement && message) {
            hintElement.textContent = message;
            hintElement.className = className;
        }
    };

    if (!receiptDateInput || !hintElement) return;

    if (!receiptDateInput.value) {
        disableLink('Please select a receipt date to proceed.', 'form-text text-muted');
        return;
    }

    const selectedDate = new Date(receiptDateInput.value);
    const today = new Date();


    selectedDate.setHours(0, 0, 0, 0);
    today.setHours(0, 0, 0, 0);


    if (currentPoInfo && currentPoInfo.createdAt) {
        const poCreated = new Date(currentPoInfo.createdAt);
        poCreated.setHours(0, 0, 0, 0);
        if (selectedDate.getTime() < poCreated.getTime()) {
            disableLink('Receipt date cannot be earlier than the purchase order creation date.');
            return;
        }
    }

  
    if (selectedDate.getTime() > today.getTime()) {
        disableLink('Receipt date cannot be in the future.');
        return;
    }

 
    if (poId && proceedLink) {
        proceedLink.href = `/StockIn/Details/${poId}`;
        proceedLink.classList.remove('disabled');
        proceedLink.removeAttribute('aria-disabled');
        proceedLink.removeAttribute('tabindex');
    }
    hintElement.textContent = 'Ready to proceed to Stock-In!';
    hintElement.className = 'form-text text-success';
}




let searchTimeout;
function handleSearch() {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        currentPage = 1; 
        loadPurchaseOrders();
    }, 500);
}

document.addEventListener('DOMContentLoaded', () => {
    if (!warehouseId) {
        alert('Error: WarehouseId not found!');
        console.error('warehouseId is undefined');
        return;
    }

    console.log('Stock In page initialized for warehouse:', warehouseId);
    loadPurchaseOrders();


    const filterStatus = document.getElementById('filter-status');
    const searchInput = document.getElementById('search-input');

    if (filterStatus) {
        filterStatus.addEventListener('change', loadPurchaseOrders);
    }

    if (searchInput) {
        searchInput.addEventListener('keyup', handleSearch);
    }
    
});
