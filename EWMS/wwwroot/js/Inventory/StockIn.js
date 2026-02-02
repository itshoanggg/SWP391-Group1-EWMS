/* =========================================================
   STOCK IN - LIST PAGE JAVASCRIPT
========================================================= */

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
    return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// Get status badge HTML
function getStatusBadge(status) {
    const statusMap = {
        'Pending': { class: 'status-pending', text: 'Chờ duyệt' },
        'Received': { class: 'status-received', text: 'Đã nhận' },
    };

    const statusInfo = statusMap[status] || { class: 'bg-secondary', text: status };
    return `<span class="badge ${statusInfo.class}">${statusInfo.text}</span>`;
}


// Load purchase orders
async function loadPurchaseOrders() {
    try {
        const status = document.getElementById('filter-status').value;
        const search = document.getElementById('search-input').value;

        const tbody = document.querySelector('#purchase-orders-table tbody');

        tbody.innerHTML = `
        <tr>
            <td colspan="7" class="text-center">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Đang tải...</span>
                </div>
            </td>
        </tr>
        `;


        const response = await fetch(
            `/StockIn/GetPurchaseOrders?warehouseId=${warehouseId}&status=${status}&search=${encodeURIComponent(search)}`
        );
        const data = await response.json();

        if (data.error) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center text-danger">
                        <i class="fas fa-exclamation-triangle"></i> Lỗi: ${data.error}
                    </td>
                </tr>
            `;
            return;
        }

        if (!data.length) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center text-muted">
                        <i class="fas fa-inbox"></i> Không có dữ liệu
                    </td>
                </tr>
            `;
            return;
        }

        tbody.innerHTML = '';
        data.forEach(order => {
            const row = document.createElement('tr');
            row.onclick = () => viewDetails(order.purchaseOrderId);
            row.innerHTML = `
                <td><strong>PO-${String(order.purchaseOrderId).padStart(4, '0')}</strong></td>
                <td>${order.supplierName}</td>
                <td class="text-end">${formatNumber(order.totalItems)}</td>
                <td class="text-end">${formatCurrency(order.totalAmount)}</td>
                <td>${order.createdBy}</td>
                <td>${getStatusBadge(order.status)}</td>
                <td>
                    <div class="action-buttons">
                        <button class="btn btn-sm btn-info" onclick="event.stopPropagation(); viewDetails(${order.purchaseOrderId})">
                            <i class="fas fa-eye"></i> Xem
                        </button>
                    </div>
                </td>
            `;
            tbody.appendChild(row);
        });
    } catch (error) {
        console.error('Load purchase orders failed:', error);
        const tbody = document.querySelector('#purchase-orders-table tbody');
        tbody.innerHTML = `
            <tr>
                <td colspan="7" class="text-center text-danger">
                    <i class="fas fa-exclamation-triangle"></i> Lỗi tải dữ liệu
                </td>
            </tr>
        `;
    }
}

// View details
function viewDetails(purchaseOrderId) {
    window.location.href = `/StockIn/Details/${purchaseOrderId}`;
}

// Search handler with debounce
let searchTimeout;
function handleSearch() {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        loadPurchaseOrders();
    }, 500);
}

// Refresh all data
async function refreshData() {
    console.log('Refreshing data for warehouse:', warehouseId);
    await Promise.all([
        loadPurchaseOrders()
    ]);
    console.log('Refresh complete');
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    if (!warehouseId) {
        alert('Lỗi: Không tìm thấy WarehouseId!');
        console.error('warehouseId is undefined');
        return;
    }

    console.log('Stock In page initialized for warehouse:', warehouseId);
    refreshData();

    // Add event listeners
    document.getElementById('filter-status').addEventListener('change', loadPurchaseOrders);
    document.getElementById('search-input').addEventListener('keyup', handleSearch);
});