/* =========================================================
   STOCK IN - INDEX PAGE JAVASCRIPT
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
    return date.toLocaleDateString('vi-VN');
}

// Get status badge HTML
function getStatusBadge(status) {
    const statusMap = {
        'InTransit': { class: 'bg-info', icon: 'fa-shipping-fast', text: 'Đang vận chuyển' },
        'Delivered': { class: 'bg-success', icon: 'fa-check-circle', text: 'Đã về kho' },
        'PartiallyReceived': { class: 'bg-primary', icon: 'fa-boxes', text: 'Nhận một phần' },
        'Received': { class: 'bg-dark', icon: 'fa-check-double', text: 'Đã nhận đủ' }
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
                    <span class="visually-hidden">Đang tải...</span>
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
                        <i class="fas fa-exclamation-triangle"></i> Dữ liệu không hợp lệ
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
                        <i class="fas fa-inbox"></i> Không có đơn hàng nào
                    </td>
                </tr>
            `;
            return;
        }

        // Render data
        tbody.innerHTML = '';
        data.forEach(order => {
            const row = document.createElement('tr');

            // Check if clickable (Delivered or PartiallyReceived)
            const isClickable = order.status === 'Delivered' || order.status === 'PartiallyReceived';

            if (isClickable) {
                row.className = 'clickable';
                row.onclick = () => viewDetails(order.purchaseOrderId);
            } else {
                row.className = 'disabled';
                row.title = 'Chỉ có thể nhập kho khi trạng thái là "Đã về kho" hoặc "Nhận một phần"';
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
                    ${order.remainingItems > 0
                    ? `<span class="badge bg-warning text-dark">${formatNumber(order.remainingItems)}</span>`
                    : '<span class="badge bg-success">Đủ</span>'}
                </td>
                <td>${getStatusBadge(order.status)}</td>
                <td>
                    ${isClickable
                    ? `<button class="btn btn-sm btn-primary" onclick="event.stopPropagation(); viewDetails(${order.purchaseOrderId})">
                            <i class="fas fa-box-open"></i> Nhập kho
                       </button>`
                    : `<button class="btn btn-sm btn-secondary" disabled>
                            <i class="fas fa-lock"></i> Chưa sẵn sàng
                       </button>`
                }
                </td>
            `;
            tbody.appendChild(row);
        });

        console.log(`Loaded ${data.length} purchase orders`);
    } catch (error) {
        console.error('Load purchase orders failed:', error);

        const tbody = document.querySelector('#purchase-orders-table tbody');
        if (tbody) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">
                        <i class="fas fa-exclamation-triangle"></i> Lỗi tải dữ liệu: ${error.message}
                        <br><small>Vui lòng mở Console (F12) để xem chi tiết</small>
                    </td>
                </tr>
            `;
        }
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
        loadPurchaseOrders();
    }, 500);
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    if (!warehouseId) {
        alert('Lỗi: Không tìm thấy WarehouseId!');
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