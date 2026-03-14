// SignalR Connection cho Sales Order
let salesOrderConnection = null;

function initSalesOrderSignalR() {
    // Chỉ khởi tạo nếu có element currentUserId (tức là đang ở trang Sales Order)
    const userIdElement = document.getElementById("currentUserId");
    if (!userIdElement) {
        console.log("currentUserId element not found - not on Sales Order page");
        return; // Không ở trang Sales Order, skip
    }

    const userId = userIdElement.value;
    console.log("Found userId element, value:", userId);
    
    if (!userId) {
        console.warn("User ID not found - userId value is empty");
        return;
    }

    console.log("Initializing SalesOrder SignalR for user:", userId);

    salesOrderConnection = new signalR.HubConnectionBuilder()
        .withUrl("/salesOrderHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000]) // Retry intervals
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Xử lý kết quả tạo Sales Order
    salesOrderConnection.on("ReceiveSalesOrderResult", function (result) {
        console.log(">>> Received Sales Order Result:", result);

        if (result.success) {
            // Hiển thị thông báo thành công (hiển thị lâu hơn)
            showNotification("success", result.message, 10000);
            
            // Redirect đến trang chi tiết sau 3 giây
            setTimeout(function() {
                console.log(">>> Redirecting to Details page...");
                window.location.href = "/SalesOrder/Details/" + result.salesOrderId;
            }, 3000);
        } else {
            // Hiển thị thông báo lỗi (hiển thị lâu hơn để đọc)
            showNotification("error", result.message, 10000);
            
            // Reload trang để tạo đơn mới
            setTimeout(function() {
                window.location.href = "/SalesOrder/Create";
            }, 5000);
        }
    });

    // Xử lý khi kết nối thành công
    salesOrderConnection.onreconnected(connectionId => {
        console.log("SignalR Reconnected. Connection ID:", connectionId);
        // Không hiển thị toast, chỉ log
    });

    // Xử lý khi đang reconnect
    salesOrderConnection.onreconnecting(error => {
        console.warn("SignalR Reconnecting...", error);
        // Không hiển thị toast, chỉ log
    });

    // Xử lý khi ngắt kết nối
    salesOrderConnection.onclose(error => {
        console.error("SignalR Connection Closed", error);
        // Không hiển thị toast, chỉ log
    });

    // Bắt đầu kết nối
    salesOrderConnection.start()
        .then(function () {
            console.log("SignalR Connected Successfully");
            
            // Join vào group của user hiện tại
            return salesOrderConnection.invoke("JoinUserGroup", userId);
        })
        .then(function() {
            console.log("Joined user group:", userId);
            // Không hiển thị toast nữa, chỉ log trong console
        })
        .catch(function (err) {
            console.error("SignalR Connection Error:", err);
            // Chỉ hiển thị lỗi nếu không connect được
            showNotification("error", "Không thể kết nối real-time: " + err.message);
        });
}

// Hàm hiển thị thông báo (Toast notification)
function showNotification(type, message, duration = 8000) {
    // Xóa toast cũ nếu có
    const existingToast = document.querySelector('.sales-order-toast');
    if (existingToast) {
        existingToast.remove();
    }

    // Tạo toast element
    const toast = document.createElement('div');
    toast.className = `sales-order-toast toast-${type}`;
    
    const icon = type === 'success' ? '✅' : 
                 type === 'error' ? '❌' : 
                 type === 'warning' ? '⚠️' : 'ℹ️';
    
    toast.innerHTML = `
        <div class="toast-content">
            <span class="toast-icon">${icon}</span>
            <span class="toast-message">${message}</span>
            <button class="toast-close" onclick="this.parentElement.parentElement.remove()">×</button>
        </div>
    `;
    
    document.body.appendChild(toast);
    
    // Auto fade in
    setTimeout(() => toast.classList.add('show'), 10);
    
    // Auto remove
    if (duration > 0) {
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    }
}

// Khởi tạo khi DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initSalesOrderSignalR);
} else {
    initSalesOrderSignalR();
}

// Cleanup khi rời khỏi trang
window.addEventListener('beforeunload', function() {
    if (salesOrderConnection) {
        salesOrderConnection.stop();
    }
});
