document.addEventListener('DOMContentLoaded', () => {
    const yearInput = document.getElementById('nxt-year');
    const monthInput = document.getElementById('nxt-month');
    const monthPicker = document.getElementById('nxt-monthpicker');

    function syncPicker() {
        const val = (monthPicker.value || '').trim(); // yyyy-MM
        if (val) {
            const [yyyy, mm] = val.split('-');
            yearInput.value = yyyy;
            monthInput.value = mm.startsWith('0') ? mm.substring(1) : mm;
        } else {
            // Nếu để trống (trình duyệt cho phép), xóa month và giữ nguyên year hiện tại
            monthInput.value = '';
        }
    }

    // init
    syncPicker();
    monthPicker.addEventListener('change', syncPicker);
});
