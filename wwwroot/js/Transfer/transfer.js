// Simple client side helper for Create Transfer page
document.addEventListener('DOMContentLoaded', function () {
    const addBtn = document.getElementById('add-product-btn');
    const select = document.getElementById('product-add-select');
    const tbody = document.querySelector('#transfer-items-table tbody');

    addBtn?.addEventListener('click', function () {
        const val = select.value;
        if (!val) return alert('Please choose a product.');

        const name = select.options[select.selectedIndex].dataset.name || select.options[select.selectedIndex].text;
        const stock = parseInt(select.options[select.selectedIndex].dataset.stock || '0');

        // Prevent adding same product twice
        if (tbody.querySelector(`tr[data-product-id="${val}"]`)) {
            return alert('Product already added.');
        }

        const row = document.createElement('tr');
        row.setAttribute('data-product-id', val);
        row.innerHTML = `
            <td>
                <input type="hidden" name="Items[index].ProductId" value="${val}" />
                <span class="fw-semibold">${name}</span>
            </td>
            <td class="align-middle">${stock}</td>
            <td>
                <input type="number" name="Items[index].Quantity" class="form-control form-control-sm qty-input" value="1" min="1" max="${stock}" required />
            </td>
            <td class="text-end align-middle">
                <button type="button" class="btn btn-sm btn-danger remove-row">Remove</button>
            </td>
        `;

        tbody.appendChild(row);
        refreshIndexing();

        // remove
        row.querySelector('.remove-row')?.addEventListener('click', () => {
            row.remove();
            refreshIndexing();
        });
    });

    function refreshIndexing() {
        const rows = Array.from(tbody.querySelectorAll('tr'));
        rows.forEach((r, i) => {
            // update input names
            const prodInput = r.querySelector('input[type="hidden"]');
            const qtyInput = r.querySelector('.qty-input');
            if (prodInput) prodInput.name = `Items[${i}].ProductId`;
            if (qtyInput) qtyInput.name = `Items[${i}].Quantity`;
        });
    }
});