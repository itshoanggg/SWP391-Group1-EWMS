using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EWMS.ViewModels
{
    public class SalesOrderViewModel
    {
        public int SalesOrderID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string WarehouseName { get; set; }
        public string Notes { get; set; }

        public List<SalesOrderProductViewModel> Products { get; set; }
    }

    public class SalesOrderProductViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class SalesOrderFilterViewModel
    {
        public string SearchTerm { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateFrom { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateTo { get; set; }
    }

    public class CreateSalesOrderRequest
    {
        [Required(ErrorMessage = "Tên khách hàng là bắt buộc")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string CustomerPhone { get; set; }

        public string CustomerAddress { get; set; }
        public string Notes { get; set; }

        public int CreatedBy { get; set; }
        public int WarehouseID { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Phải có ít nhất một sản phẩm")]
        public List<SalesOrderItemRequest> Products { get; set; }
    }

    public class SalesOrderItemRequest
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class ProductForSaleViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public int TotalStock { get; set; }
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; }
    }

    public class SupplierViewModel
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
    }

    public class CreateSalesOrderResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int OrderId { get; set; }
        public List<PurchaseRequestInfo> PurchaseRequests { get; set; }
    }

    public class PurchaseRequestInfo
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int QuantityNeeded { get; set; }
        public int CurrentStock { get; set; }
        public int ShortageQuantity { get; set; }
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
    }
}