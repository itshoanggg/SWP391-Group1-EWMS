// Product Management JavaScript - Markup Calculation & Validation

// Category markup suggestions (based on Vietnamese electronics retail margins)
const categoryMarkupDefaults = {
    1: { percent: 25, description: 'Laptops: typical 20-30% markup (Dell standard margin)' },
    2: { percent: 21, description: 'Smartphones: Apple-controlled margin ~20-22%' },
    3: { percent: 44, description: 'Audio: Sony accessories ~40-50% margin' },
    4: { percent: 41, description: 'Monitors: Samsung displays ~35-45% markup' },
    5: { percent: 56, description: 'Accessories: Logitech peripherals ~50-60% markup' }
};

// Active state
let activeMarkup = 25;
let manualSellPrice = false;

// Format currency (Vietnamese Dong)
function formatCurrency(value) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND',
        minimumFractionDigits: 0
    }).format(value);
}

// Calculate margin percentage
function calculateMargin(cost, sell) {
    if (sell <= 0) return 0;
    return ((sell - cost) / sell * 100).toFixed(1);
}

// Get selected markup percentage
function getSelectedMarkup() {
    const customInput = document.getElementById('custom-markup-input');
    if (customInput && customInput.style.display !== 'none') {
        return parseFloat(customInput.value) || 0;
    }
    return activeMarkup;
}

// Apply markup to cost price
function applyMarkup() {
    const costInput = document.getElementById('CostPrice');
    const sellInput = document.getElementById('SellingPrice');
    
    if (!costInput || !sellInput) return;

    const cost = getNumericValue(costInput);
    const markup = getSelectedMarkup();

    if (cost > 0 && markup > 0) {
        const suggestedSell = Math.round(cost * (1 + markup / 100));
        sellInput.value = suggestedSell.toLocaleString('en-US');
        manualSellPrice = false;
    }

    updatePreview();
    updateMarkupHint();
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
    if (prevMargin) prevMargin.textContent = calculateMargin(cost, sell) + '%';
    if (prevProfit) prevProfit.textContent = formatCurrency(Math.max(0, sell - cost));
}

// Select markup chip
function selectMarkup(chip, percent) {
    // Remove active class from all chips
    document.querySelectorAll('.markup-chip').forEach(c => c.classList.remove('selected'));
    
    // Add active class to clicked chip
    chip.classList.add('selected');

    const customRow = document.getElementById('custom-markup-row');
    
    if (percent === 'custom') {
        // Show custom input
        if (customRow) customRow.style.display = 'block';
    } else {
        // Hide custom input and set markup
        if (customRow) customRow.style.display = 'none';
        activeMarkup = percent;
        manualSellPrice = false;
        applyMarkup();
    }
}

// Update markup hint text
function updateMarkupHint() {
    const hint = document.getElementById('markup-hint');
    if (!hint) return;

    const cost = parseFloat(document.getElementById('CostPrice')?.value) || 0;
    const markup = getSelectedMarkup();
    const multiplier = (1 + markup / 100).toFixed(2);

    hint.textContent = `Selling = Cost × (1 + ${markup}%) = ${formatCurrency(cost)} × ${multiplier}`;
}

// Handle category change
function onCategoryChange() {
    const categorySelect = document.getElementById('CategoryId');
    if (!categorySelect) return;

    const categoryId = parseInt(categorySelect.value);
    
    // Fetch category details via AJAX
    fetch(`/Product/GetCategoryDetails?categoryId=${categoryId}`)
        .then(response => response.json())
        .then(data => {
            // Update supplier display
            const supplierInput = document.getElementById('supplier-display');
            if (supplierInput) {
                supplierInput.value = data.supplierName || 'N/A';
            }

            // Update markup suggestion
            if (data.suggestedMarkup) {
                preselectChip(data.suggestedMarkup);
                activeMarkup = data.suggestedMarkup;
                
                // Update hint
                const hint = document.getElementById('markup-hint');
                if (hint) {
                    const description = categoryMarkupDefaults[categoryId]?.description || 
                                      `Suggested markup for this category: ${data.suggestedMarkup}%`;
                    hint.textContent = description;
                }

                // Apply new markup if not manually set
                if (!manualSellPrice) {
                    applyMarkup();
                }
            }
        })
        .catch(error => {
            console.error('Error fetching category details:', error);
        });
}

// Preselect markup chip based on percentage
function preselectChip(percent) {
    const chips = document.querySelectorAll('.markup-chip');
    let found = false;

    chips.forEach(chip => {
        chip.classList.remove('selected');
        const chipPercent = parseInt(chip.dataset.percent);
        
        if (chipPercent === percent) {
            chip.classList.add('selected');
            found = true;
            activeMarkup = percent;
        }
    });

    // If exact match not found, select custom
    if (!found) {
        const customChip = document.querySelector('.markup-chip[data-percent="custom"]');
        if (customChip) {
            customChip.classList.add('selected');
            const customRow = document.getElementById('custom-markup-row');
            const customInput = document.getElementById('custom-markup-input');
            if (customRow) customRow.style.display = 'block';
            if (customInput) customInput.value = percent;
        }
    }
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
    const autoButton = document.getElementById('auto-markup-btn');
    const customInput = document.getElementById('custom-markup-input');

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

    if (autoButton) {
        autoButton.addEventListener('click', function(e) {
            e.preventDefault();
            manualSellPrice = false;
            applyMarkup();
        });
    }

    if (customInput) {
        customInput.addEventListener('input', function() {
            activeMarkup = parseFloat(this.value) || 0;
            applyMarkup();
        });
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
