/* =========================================================
   STOCK IN - HISTORY PAGE JAVASCRIPT
========================================================= */

let currentPage = 1;
let pageSize = 10;
let totalItems = 0;
let allOrders = [];

function formatNumber(value) {
    return new Intl.NumberFormat('vi-VN').format(value);
}

function formatDate(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB');
}

function getStatusBadge(status) {
    const statusMap = {
        'Received': { class: 'bg-dark', icon: 'fa-check-double', text: 'Fully Received' },
        'Cancelled': { class: 'bg-danger', icon: 'fa-ban', text: 'Cancelled' }
    };

    const statusInfo = statusMap[status] || { class: 'bg-secondary', icon: 'fa-question', text: status };
    return `<span class="badge ${statusInfo.class}"><i class="fas ${statusInfo.icon}"></i> ${statusInfo.text}</span>`;
}

async function loadHistoryPurchaseOrders() {
    try {
        const search = document.getElementById('search-input')?.value || '';
        const tbody = document.querySelector('#purchase-orders-table tbody');

        if (!tbody) return;

        tbody.innerHTML = `
        <tr>
            <td colspan=\"8\" class=\"text-center\">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
            </td>
        </tr>`;

        const urlReceived = `/StockIn/GetPurchaseOrders?warehouseId=${warehouseId}&status=Received&search=${encodeURIComponent(search)}`;
        const urlCancelled = `/StockIn/GetPurchaseOrders?warehouseId=${warehouseId}&status=Cancelled&search=${encodeURIComponent(search)}`;

        const [resReceived, resCancelled] = await Promise.all([fetch(urlReceived), fetch(urlCancelled)]);
        if (!resReceived.ok || !resCancelled.ok) throw new Error('HTTP error loading history');

        const [dataReceived, dataCancelled] = await Promise.all([resReceived.json(), resCancelled.json()]);
        const listReceived = Array.isArray(dataReceived) ? dataReceived : [];
        const listCancelled = Array.isArray(dataCancelled) ? dataCancelled : [];

        let filtered = [...listReceived, ...listCancelled];
        // Sort by lastReceivedDate (desc), fallback to createdAt
        filtered.sort((a, b) => {
            const da = a.lastReceivedDate ? new Date(a.lastReceivedDate) : (a.createdAt ? new Date(a.createdAt) : 0);
            const db = b.lastReceivedDate ? new Date(b.lastReceivedDate) : (b.createdAt ? new Date(b.createdAt) : 0);
            return db - da;
        });

        if (filtered.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan=\"8\" class=\"text-center text-muted\">
                        <i class="fas fa-inbox"></i> No history purchase orders found
                    </td>
                </tr>`;
            updatePaginationInfo(0, 0, 0);
            renderPagination(0);
            return;
        }

        allOrders = filtered;
        totalItems = filtered.length;
        const totalPages = Math.ceil(totalItems / pageSize);
        if (currentPage > totalPages) currentPage = 1;

        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = startIndex + pageSize;
        const pageData = allOrders.slice(startIndex, endIndex);

        renderTableRows(pageData, tbody);
        updatePaginationInfo(startIndex + 1, Math.min(endIndex, totalItems), totalItems);
        renderPagination(totalPages);
    } catch (err) {
        console.error('Failed loading history POs:', err);
        const tbody = document.querySelector('#purchase-orders-table tbody');
        if (tbody) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">
                        <i class="fas fa-exclamation-triangle"></i> Failed to load history: ${err.message}
                    </td>
                </tr>`;
        }
    }
}

function renderTableRows(orders, tbody) {
    tbody.innerHTML = '';
    orders.forEach(order => {
        const row = document.createElement('tr');
        const expectedDateDisplay = order.expectedReceivingDate ? formatDate(order.expectedReceivingDate) : 'N/A';
        row.innerHTML = `
            <td><strong>PO-${String(order.purchaseOrderId).padStart(4, '0')}</strong></td>
            <td>${order.supplierName || 'N/A'}</td>
            <td>${expectedDateDisplay}</td>
            <td class="text-end">${formatNumber(order.totalItems)}</td>
            <td class="text-end">${formatNumber(order.receivedItems)}</td>
            
            <td>${getStatusBadge(order.status)}</td>
            <td>
                ${order.status === 'Cancelled'
                    ? `<button class="btn btn-sm btn-danger" disabled>
                           <i class="fas fa-ban"></i> Cancelled
                       </button>`
                    : `<a class="btn btn-sm btn-outline-primary" href="/StockIn/DetailsReadOnly/${order.purchaseOrderId}">
                           <i class="fas fa-eye"></i> View
                       </a>`}
            </td>
        `;
        tbody.appendChild(row);
    });
}

function updatePaginationInfo(start, end, total) {
    const infoElement = document.getElementById('pagination-info');
    if (!infoElement) return;
    const spanElement = infoElement.querySelector('span:last-child');
    if (spanElement) {
        spanElement.textContent = `Showing ${start} to ${end} of ${total} entries`;
    }
}

function renderPagination(totalPages) {
    const paginationElement = document.getElementById('pagination-controls');
    if (!paginationElement) return;
    if (totalPages <= 1) { paginationElement.innerHTML = ''; return; }

    let html = '<ul class="pagination mb-0">';
    html += `
        <li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage - 1}); return false;">Previous</a>
        </li>`;

    const maxVisible = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
    let endPage = Math.min(totalPages, startPage + maxVisible - 1);
    if (endPage - startPage < maxVisible - 1) startPage = Math.max(1, endPage - maxVisible + 1);

    for (let i = startPage; i <= endPage; i++) {
        html += `
            <li class="page-item ${i === currentPage ? 'active' : ''}">
                <a class="page-link" href="#" onclick="changePage(${i}); return false;">${i}</a>
            </li>`;
    }

    html += `
        <li class="page-item ${currentPage === totalPages ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage + 1}); return false;">Next</a>
        </li>`;
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

let searchTimeout;
function handleSearch() {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        currentPage = 1;
        loadHistoryPurchaseOrders();
    }, 500);
}

document.addEventListener('DOMContentLoaded', () => {
    if (!warehouseId) {
        alert('Error: WarehouseId not found!');
        console.error('warehouseId is undefined');
        return;
    }
    loadHistoryPurchaseOrders();

    const searchInput = document.getElementById('search-input');
    if (searchInput) {
        searchInput.addEventListener('keyup', handleSearch);
    }
});
