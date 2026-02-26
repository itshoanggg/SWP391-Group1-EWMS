/* =========================================================
   STOCK MANAGEMENT - JAVASCRIPT
========================================================= */

const warehouseId = window.warehouseId;
let selectedRack = window.selectedRack;

let racksData = [];
let locationsData = [];
let productsData = [];
let selectedLocationId = null;

// Format helpers
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

/* =========================================================
   LOAD DATA
========================================================= */

// Summary cards removed from view - function no longer needed

async function loadRacks() {
    try {
        const response = await fetch(`/Stock/GetRacks?warehouseId=${warehouseId}`);
        const data = await response.json();
        if (data.error) { console.error('Racks error:', data.error); return; }

        racksData = data;

        // Auto select rack từ URL hoặc rack đầu tiên
        if (racksData.length > 0) {
            const rackToSelect = racksData.find(r => r.rack === selectedRack)
                ? selectedRack
                : racksData[0].rack;

            selectedRack = rackToSelect;
            await selectRack(rackToSelect, true);
        }
    } catch (error) {
        console.error('Load racks failed:', error);
    }
}

async function loadLocations(rack) {
    try {
        const response = await fetch(`/Stock/GetLocationsByRack?warehouseId=${warehouseId}&rack=${rack}`);
        const data = await response.json();
        if (data.error) { console.error('Locations error:', data.error); return []; }

        locationsData = data;
        renderNavLocations(rack);
        return data;
    } catch (error) {
        console.error('Load locations failed:', error);
        return [];
    }
}

async function loadProducts(locationId) {
    try {
        const container = document.getElementById('products-container');
        container.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Đang tải...</span>
                </div>
            </div>`;

        const response = await fetch(`/Stock/GetProductsByLocation?locationId=${locationId}`);
        const data = await response.json();
        if (data.error) {
            container.innerHTML = '<div class="alert alert-danger">Error loading data</div>';
            return;
        }

        productsData = data;
        renderProducts();
    } catch (error) {
        console.error('Load products failed:', error);
    }
}

/* =========================================================
   RENDER NAV - RACK & LOCATION
   Stock.js render locations vào nav khi đang ở trang Stock
========================================================= */

function renderNavLocations(rack) {
    // Tìm container location của rack trong nav (được tạo bởi _Layout.cshtml)
    const locContainer = document.getElementById(`nav-loc-container-${rack}`);
    if (!locContainer) return;

    locContainer.innerHTML = '';

    if (locationsData.length === 0) {
        locContainer.innerHTML = `
            <div class="submenu-location-item" style="opacity:0.5; cursor:default;">
                Không có vị trí
            </div>`;
        locContainer.style.display = 'block';
        return;
    }

    locationsData.forEach(location => {
        const utilizationPercent = location.capacity > 0
            ? Math.round(location.currentStock / location.capacity * 100)
            : 0;

        const badgeColor = utilizationPercent > 80 ? 'bg-danger'
            : utilizationPercent > 50 ? 'bg-warning' : 'bg-success';

        const a = document.createElement('a');
        a.href = '#';
        a.className = 'submenu-location-item';
        a.id = `nav-loc-${location.locationId}`;
        a.innerHTML = `
            <span><i class="bi bi-geo-alt me-1"></i>${location.locationCode}</span>
            <span class="badge ${badgeColor}" style="font-size:0.65rem;">${utilizationPercent}%</span>
        `;
        a.addEventListener('click', function (e) {
            e.preventDefault();
            // Bỏ active tất cả location
            document.querySelectorAll('.submenu-location-item').forEach(el => el.classList.remove('active'));
            a.classList.add('active');
            selectLocation(location.locationId, location.locationCode, rack);
        });

        locContainer.appendChild(a);
    });

    // Hiện container + xoay chevron
    locContainer.style.display = 'block';
    const chevron = document.getElementById(`nav-chevron-${rack}`);
    if (chevron) chevron.style.transform = 'rotate(180deg)';

    // Active rack link
    const rackLink = document.getElementById(`nav-rack-${rack}`);
    if (rackLink) rackLink.classList.add('active');
}

/* =========================================================
   RENDER PRODUCTS (main content)
========================================================= */

function renderProducts() {
    const container = document.getElementById('products-container');
    const badge = document.getElementById('product-count-badge');
    if (badge) badge.textContent = `${productsData.length} products`;

    if (productsData.length === 0) {
        container.innerHTML = `
            <div class="text-center py-5 text-muted">
                <i class="bi bi-inbox" style="font-size: 4rem;"></i>
                <p class="mt-3">There are no products in this location.</p>
            </div>`;
        return;
    }

    container.innerHTML = '';
    const colors = ['#667eea', '#f093fb', '#4facfe', '#fa709a', '#43e97b'];

    productsData.forEach((product, index) => {
        const card = document.createElement('div');
        card.className = 'card stock-card mb-3';
        card.style.borderLeftColor = colors[index % colors.length];

        card.innerHTML = `
            <div class="card-body">
                <div class="row align-items-center">
                    <div class="col-md-1 text-center">
                        <div class="badge bg-primary" style="font-size: 1rem; padding: 10px 8px; word-break: break-all;">
                            ${product.sku}
                        </div>
                    </div>
                    <div class="col-md-4">
                        <h6 class="mb-1">${product.productName}</h6>
                        <small class="text-muted">
                            <i class="bi bi-tag"></i> ${product.categoryName}
                        </small>
                    </div>
                    <div class="col-md-3">
                        <small class="text-muted d-block">Số lượng tồn</small>
                        <h4 class="mb-0 text-success">${formatNumber(product.quantity)}</h4>
                    </div>
                    <div class="col-md-4">
                        <small class="text-muted d-block">Cập nhật lần cuối</small>
                        <small>${formatDate(product.lastUpdated)}</small>
                    </div>
                </div>
            </div>
        `;
        container.appendChild(card);
    });
}

/* =========================================================
   USER ACTIONS
========================================================= */

async function selectRack(rack, autoSelectFirstLocation = false) {
    const locContainer = document.getElementById(`nav-loc-container-${rack}`);
    const isAlreadyOpen = locContainer && locContainer.style.display !== 'none'
        && locContainer.innerHTML.trim() !== '';

    // Toggle đóng nếu đang mở và không phải auto select
    if (isAlreadyOpen && !autoSelectFirstLocation) {
        locContainer.style.display = 'none';
        const chevron = document.getElementById(`nav-chevron-${rack}`);
        if (chevron) chevron.style.transform = 'rotate(0deg)';
        return;
    }

    // Đóng tất cả rack khác
    racksData.forEach(r => {
        if (r.rack !== rack) {
            const otherLoc = document.getElementById(`nav-loc-container-${r.rack}`);
            const otherChevron = document.getElementById(`nav-chevron-${r.rack}`);
            const otherLink = document.getElementById(`nav-rack-${r.rack}`);
            if (otherLoc) otherLoc.style.display = 'none';
            if (otherChevron) otherChevron.style.transform = 'rotate(0deg)';
            if (otherLink) otherLink.classList.remove('active');
        }
    });

    selectedRack = rack;

    // Update breadcrumb header
    document.getElementById('header-rack').textContent = rack;
    document.getElementById('header-location').textContent = '--';

    // Load locations → tự render vào nav qua renderNavLocations()
    await loadLocations(rack);

    // Auto select location đầu tiên
    if (autoSelectFirstLocation && locationsData.length > 0) {
        const firstLoc = locationsData[0];

        // Highlight location đầu tiên trong nav
        document.querySelectorAll('.submenu-location-item').forEach(el => el.classList.remove('active'));
        const firstLocLink = document.getElementById(`nav-loc-${firstLoc.locationId}`);
        if (firstLocLink) firstLocLink.classList.add('active');

        selectLocation(firstLoc.locationId, firstLoc.locationCode, rack);
    }
}

function selectLocation(locationId, locationCode, rack) {
    selectedLocationId = locationId;

    // Update breadcrumb header
    document.getElementById('header-rack').textContent = rack || selectedRack;
    document.getElementById('header-location').textContent = locationCode;

    // Update title trong card products
    document.getElementById('current-location-title').textContent =
        `Rack ${rack || selectedRack} › ${locationCode}`;

    loadProducts(locationId);
}

async function refreshAll() {
    await loadRacks();
}

/* =========================================================
   INIT
========================================================= */
document.addEventListener('DOMContentLoaded', async () => {
    console.log('Stock Management initialized for warehouse:', warehouseId);
    console.log('Default rack:', selectedRack);

    if (!warehouseId || warehouseId === 0) {
        console.error('ERROR: warehouseId is not defined!');
        alert('Lỗi: Không xác định được kho!');
        return;
    }

    await refreshAll();
});