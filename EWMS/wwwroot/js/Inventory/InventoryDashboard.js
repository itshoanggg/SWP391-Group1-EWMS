let charts = {};
const warehouseId = window.warehouseId;

/* =======================
   FORMAT HELPERS
======================= */
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
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

/* =======================
   KPI
======================= */
async function loadKPIMetrics() {
    try {
        const res = await fetch(`/InventoryDashboard/GetKPIMetrics?warehouseId=${warehouseId}`);
        const data = await res.json();

        if (data.error) {
            console.error('KPI Error:', data.error);
            return;
        }

        document.getElementById('kpi-revenue').textContent = formatCurrency(data.revenue);
        document.getElementById('kpi-profit').textContent = formatCurrency(data.profit);
        document.getElementById('kpi-orders').textContent = data.todayOrders;
        document.getElementById('kpi-inventory').textContent = formatCurrency(data.inventoryValue);
    } catch (error) {
        console.error('Load KPI failed:', error);
    }
}

/* =======================
   KPI CARDS (New)
======================= */
async function loadKPICards() {
    try {
        const res = await fetch(`/InventoryDashboard/GetInventoryStats?warehouseId=${warehouseId}`);
        const data = await res.json();

        if (data.error) {
            console.error('KPI Cards Error:', data.error);
            return;
        }

        // Update KPI values
        document.getElementById('total-products').textContent = formatNumber(data.totalProducts || 0);
        document.getElementById('total-stock').textContent = formatNumber(data.totalStock || 0);
        document.getElementById('low-stock-count').textContent = formatNumber(data.lowStockCount || 0);
        document.getElementById('out-of-stock-count').textContent = formatNumber(data.outOfStockCount || 0);
    } catch (error) {
        console.error('Load KPI Cards failed:', error);
        // Set default values on error
        document.getElementById('total-products').textContent = '0';
        document.getElementById('total-stock').textContent = '0';
        document.getElementById('low-stock-count').textContent = '0';
        document.getElementById('out-of-stock-count').textContent = '0';
    }
}

/* =======================
   STOCK MOVEMENT
======================= */
async function updateStockMovement(period = 'week', btn) {
    try {
        const res = await fetch(`/InventoryDashboard/GetStockMovement?warehouseId=${warehouseId}&period=${period}`);
        const data = await res.json();

        if (data.error) {
            console.error('Stock Movement Error:', data.error);
            return;
        }

        if (charts.stockMovement) charts.stockMovement.destroy();

        charts.stockMovement = new Chart(
            document.getElementById('stockMovementChart'),
            {
                type: 'bar',
                data: {
                    labels: data.map(x => x.date),
                    datasets: [
                        {
                            label: 'Nhập',
                            data: data.map(x => x.stockIn),
                            backgroundColor: 'rgba(54, 162, 235, 0.6)',
                            borderColor: 'rgba(54, 162, 235, 1)',
                            borderWidth: 1
                        },
                        {
                            label: 'Xuất',
                            data: data.map(x => x.stockOut),
                            backgroundColor: 'rgba(255, 99, 132, 0.6)',
                            borderColor: 'rgba(255, 99, 132, 1)',
                            borderWidth: 1
                        },
                        {
                            label: 'Tồn',
                            data: data.map(x => x.stock),
                            backgroundColor: 'rgba(75, 192, 192, 0.6)',
                            borderColor: 'rgba(75, 192, 192, 1)',
                            borderWidth: 1
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: { callback: v => formatNumber(v) }
                        }
                    },
                    plugins: {
                        legend: {
                            display: true,
                            position: 'top'
                        }
                    }
                }
            }
        );

        if (btn) {
            document.querySelectorAll('.btn-group .btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
        }
    } catch (error) {
        console.error('Update Stock Movement failed:', error);
    }
}

/* =======================
   SALES & REVENUE
======================= */
async function updateSalesRevenue(period = 'month') {
    try {
        const res = await fetch(`/InventoryDashboard/GetSalesRevenue?warehouseId=${warehouseId}&period=${period}`);
        const data = await res.json();

        if (data.error) {
            console.error('Sales Revenue Error:', data.error);
            return;
        }

        if (charts.salesRevenue) charts.salesRevenue.destroy();

        charts.salesRevenue = new Chart(
            document.getElementById('salesRevenueChart'),
            {
                type: 'line',
                data: {
                    labels: data.map(x => x.date),
                    datasets: [
                        {
                            label: 'Số đơn',
                            data: data.map(x => x.quantity),
                            yAxisID: 'y',
                            borderColor: 'rgba(54, 162, 235, 1)',
                            backgroundColor: 'rgba(54, 162, 235, 0.2)',
                            tension: 0.4
                        },
                        {
                            label: 'Doanh thu (triệu)',
                            data: data.map(x => x.revenue / 1_000_000),
                            yAxisID: 'y1',
                            borderColor: 'rgba(255, 159, 64, 1)',
                            backgroundColor: 'rgba(255, 159, 64, 0.2)',
                            tension: 0.4
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    interaction: { mode: 'index', intersect: false },
                    scales: {
                        y: {
                            beginAtZero: true,
                            position: 'left',
                            title: { display: true, text: 'Số đơn' }
                        },
                        y1: {
                            beginAtZero: true,
                            position: 'right',
                            grid: { drawOnChartArea: false },
                            title: { display: true, text: 'Doanh thu (triệu VNĐ)' }
                        }
                    }
                }
            }
        );
    } catch (error) {
        console.error('Update Sales Revenue failed:', error);
    }
}

/* =======================
   BUSINESS TREND
======================= */
async function loadBusinessTrend() {
    try {
        const res = await fetch(`/InventoryDashboard/GetSalesRevenue?warehouseId=${warehouseId}&period=year`);
        const data = await res.json();

        if (data.error) {
            console.error('Business Trend Error:', data.error);
            return;
        }

        if (charts.businessTrend) charts.businessTrend.destroy();

        charts.businessTrend = new Chart(
            document.getElementById('businessTrendChart'),
            {
                type: 'line',
                data: {
                    labels: data.map(x => x.date),
                    datasets: [{
                        data: data.map(x => x.revenue / 1_000_000),
                        label: 'Doanh thu (triệu)',
                        tension: 0.4,
                        borderColor: 'rgba(75, 192, 192, 1)',
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        fill: true
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: { callback: v => formatNumber(v) + ' triệu' }
                        }
                    }
                }
            }
        );
    } catch (error) {
        console.error('Load Business Trend failed:', error);
    }
}

/* =======================
   CATEGORY
======================= */
async function loadCategoryDistribution() {
    try {
        const res = await fetch(`/InventoryDashboard/GetCategoryDistribution?warehouseId=${warehouseId}`);
        const data = await res.json();

        if (data.error) {
            console.error('Category Error:', data.error);
            return;
        }

        if (charts.category) charts.category.destroy();

        charts.category = new Chart(
            document.getElementById('categoryChart'),
            {
                type: 'doughnut',
                data: {
                    labels: data.map(x => x.category),
                    datasets: [{
                        data: data.map(x => x.quantity),
                        backgroundColor: [
                            'rgba(255, 99, 132, 0.8)',
                            'rgba(54, 162, 235, 0.8)',
                            'rgba(255, 206, 86, 0.8)',
                            'rgba(75, 192, 192, 0.8)',
                            'rgba(153, 102, 255, 0.8)'
                        ],
                        borderWidth: 2
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    plugins: {
                        legend: {
                            position: 'bottom'
                        }
                    }
                }
            }
        );
    } catch (error) {
        console.error('Load Category Distribution failed:', error);
    }
}

/* =======================
   ACTIVITIES
======================= */
async function loadRecentActivities() {
    try {
        const res = await fetch(`/InventoryDashboard/GetRecentActivities?warehouseId=${warehouseId}&limit=10`);
        const data = await res.json();

        if (data.error) {
            console.error('Activities Error:', data.error);
            return;
        }

        const tbody = document.querySelector('#activities-table tbody');
        tbody.innerHTML = '';

        if (!data.length) {
            tbody.innerHTML = `<tr><td colspan="5" class="text-center">Không có dữ liệu</td></tr>`;
            return;
        }

        data.forEach(a => {
            tbody.innerHTML += `
                <tr>
                    <td><span class="badge ${a.type === 'Nhập kho' ? 'bg-success' : 'bg-info'}">${a.type}</span></td>
                    <td>${a.description}</td>
                    <td>${a.user ?? 'N/A'}</td>
                    <td>${formatDate(a.date)}</td>
                    <td class="text-end">${formatCurrency(a.amount)}</td>
                </tr>
            `;
        });
    } catch (error) {
        console.error('Load Recent Activities failed:', error);
    }
}

/* =======================
   LOW STOCK
======================= */
async function loadLowStockAlerts() {
    try {
        const res = await fetch(`/InventoryDashboard/GetLowStockAlerts?warehouseId=${warehouseId}&threshold=10`);
        const data = await res.json();

        if (data.error) {
            console.error('Low Stock Error:', data.error);
            return;
        }

        const list = document.getElementById('low-stock-list');
        list.innerHTML = '';

        if (!data.length) {
            list.innerHTML = `<div class="alert alert-success m-0">✅ Không có cảnh báo</div>`;
            return;
        }

        data.forEach(p => {
            list.innerHTML += `
                <div class="list-group-item d-flex justify-content-between align-items-center">
                    <div>
                        <strong>${p.productName}</strong><br/>
                        <small class="text-muted"><i class="fas fa-map-marker-alt"></i> ${p.locationCode}</small>
                    </div>
                    <span class="badge bg-danger rounded-pill">${p.quantity}</span>
                </div>
            `;
        });
    } catch (error) {
        console.error('Load Low Stock Alerts failed:', error);
    }
}

/* =======================
   REFRESH
======================= */
async function refreshDashboard() {
    console.log('Refreshing dashboard for warehouse:', warehouseId);

    await Promise.all([
        loadKPICards(),
        loadKPIMetrics(),
        updateStockMovement('week'),
        updateSalesRevenue('month'),
        loadBusinessTrend(),
        loadCategoryDistribution(),
        loadRecentActivities(),
        loadLowStockAlerts()
    ]);

    console.log('Dashboard refresh complete');
}

document.addEventListener('DOMContentLoaded', () => {
    if (!warehouseId) {
        alert('Lỗi: Không tìm thấy WarehouseId!');
        console.error('warehouseId is undefined');
        return;
    }

    console.log('Dashboard initialized for warehouse:', warehouseId);
    refreshDashboard();

    // Auto refresh every 5 minutes
    setInterval(refreshDashboard, 300000);
});