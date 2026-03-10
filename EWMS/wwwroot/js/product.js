// Product Management JavaScript - Simple Auto-calculation

// Fixed markup percentage (30%)
const DEFAULT_MARKUP = 30;

// Track if user manually changed selling price
let manualSellPrice = false;

// Format currency (Vietnamese Dong)
function formatCurrency(value) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND',
        minimumFractionDigits: 0
    }).format(value);
}

// Calculate markup percentage
function calculateMarkup(cost, sell) {
    if (cost <= 0) return 0;
    return ((sell - cost) / cost * 100).toFixed(1);
}

// Get markup percentage (always use default)
function getSelectedMarkup() {
    return DEFAULT_MARKUP;
}

// Apply markup to cost price
function applyMarkup() {
    const costInput = document.getElementById('CostPrice');
    const sellInput = document.getElementById('SellingPrice');
    
    if (!costInput || !sellInput) return;

    const cost = getNumericValue(costInput);
    const markup = DEFAULT_MARKUP;

    if (cost > 0) {
        const suggestedSell = Math.round(cost * (1 + markup / 100));
        sellInput.value = suggestedSell;
        manualSellPrice = false;
    }

    updatePreview();
}

// Handle cost price change
function onCostChange() {
    if (!manualSellPrice) {
        applyMarkup();
    } else {
        updatePreview();
    }
}

// Handle manual selling price input
function onSellManual() {
    manualSellPrice = true;
    updatePreview();
}

// Update price preview
function updatePreview() {
    const costInput = document.getElementById('CostPrice');
    const sellInput = document.getElementById('SellingPrice');
    
    if (!costInput || !sellInput) return;

    const cost = getNumericValue(costInput);
    const sell = getNumericValue(sellInput);

    // Update preview elements
    const prevCost = document.getElementById('prev-cost');
    const prevSell = document.getElementById('prev-sell');
    const prevMargin = document.getElementById('prev-margin');
    const prevProfit = document.getElementById('prev-profit');

    if (prevCost) prevCost.textContent = formatCurrency(cost);
    if (prevSell) prevSell.textContent = formatCurrency(sell);
    if (prevMargin) prevMargin.textContent = calculateMarkup(cost, sell) + '%';
    if (prevProfit) prevProfit.textContent = formatCurrency(Math.max(0, sell - cost));
}


// Handle category change (simplified - just update supplier display)
function onCategoryChange() {
    const categorySelect = document.getElementById('CategoryId');
    if (!categorySelect) return;

    const categoryId = parseInt(categorySelect.value);
    
    // Return early if no category selected
    if (!categoryId) {
        const supplierInput = document.getElementById('supplier-display');
        if (supplierInput) {
            supplierInput.value = '';
        }
        return;
    }
    
    // Fetch category details via AJAX
    fetch(`/Product/GetCategoryDetails?categoryId=${categoryId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            // Update supplier display
            const supplierInput = document.getElementById('supplier-display');
            if (supplierInput) {
                supplierInput.value = data.supplierName || 'N/A';
            }
        })
        .catch(error => {
            console.error('Error fetching category details:', error);
        });
}

// Format number input with thousand separators
function formatNumberInput(input) {
    let value = input.value.replace(/,/g, '');
    if (value && !isNaN(value) && value !== '') {
        const numValue = parseFloat(value);
        if (numValue > 0) {
            input.value = numValue.toLocaleString('en-US');
        }
    }
}

// Get numeric value from formatted input
function getNumericValue(input) {
    if (!input || !input.value) return 0;
    const value = input.value.toString().replace(/,/g, '');
    return parseFloat(value) || 0;
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    // Attach event listeners
    const costInput = document.getElementById('CostPrice');
    const sellInput = document.getElementById('SellingPrice');
    const categorySelect = document.getElementById('CategoryId');

    if (costInput) {
        costInput.addEventListener('input', onCostChange);
    }

    if (sellInput) {
        sellInput.addEventListener('input', onSellManual);
    }

    if (categorySelect) {
        categorySelect.addEventListener('change', onCategoryChange);
        
        // Trigger initial load if category is selected
        if (categorySelect.value) {
            onCategoryChange();
        }
    }

    // Initial preview update
    updatePreview();
});

// Delete confirmation
function confirmDelete(productName) {
    return confirm(`Are you sure you want to delete "${productName}"?\n\nThis action cannot be undone. Make sure there is no inventory or active orders linked to this product.`);
}

// Table filtering (client-side enhancement)
function filterTable() {
    const searchInput = document.getElementById('search-input');
    const categoryFilter = document.getElementById('category-filter');
    const supplierFilter = document.getElementById('supplier-filter');

    if (!searchInput) return;

    const searchTerm = searchInput.value.toLowerCase();
    const categoryValue = categoryFilter?.value || '';
    const supplierValue = supplierFilter?.value || '';

    const rows = document.querySelectorAll('tbody tr');
    
    rows.forEach(row => {
        const productName = row.querySelector('.product-name')?.textContent.toLowerCase() || '';
        const category = row.querySelector('[data-category]')?.textContent || '';
        const supplier = row.querySelector('[data-supplier]')?.textContent || '';

        const matchSearch = !searchTerm || productName.includes(searchTerm);
        const matchCategory = !categoryValue || category === categoryValue;
        const matchSupplier = !supplierValue || supplier === supplierValue;

        if (matchSearch && matchCategory && matchSupplier) {
            row.style.display = '';
        } else {
            row.style.display = 'none';
        }
    });
}

// Form validation enhancement
function validateProductForm() {
    const productName = document.getElementById('ProductName')?.value.trim();
    const categoryId = document.getElementById('CategoryId')?.value;
    const unit = document.getElementById('Unit')?.value.trim();
    const cost = parseFloat(document.getElementById('CostPrice')?.value) || 0;
    const sell = parseFloat(document.getElementById('SellingPrice')?.value) || 0;

    let errors = [];

    if (!productName) {
        errors.push('Product name is required');
    }

    if (!categoryId) {
        errors.push('Please select a category');
    }

    if (!unit) {
        errors.push('Unit is required');
    }

    if (cost <= 0) {
        errors.push('Cost price must be greater than 0');
    }

    if (sell <= 0) {
        errors.push('Selling price must be greater than 0');
    }

    if (sell < cost) {
        errors.push('Selling price should be greater than or equal to cost price');
    }

    if (errors.length > 0) {
        alert('Please fix the following errors:\n\n' + errors.join('\n'));
        return false;
    }

    return true;
}
