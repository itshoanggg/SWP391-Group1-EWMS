create database EWMS
go 
use EWMS



/* =========================================================
   IMPROVED DATABASE SCHEMA FOR EWMS - FIXED VERSION
========================================================= */

/* =========================================================
   1. ROLES
========================================================= */
CREATE TABLE Roles (
    RoleID INT IDENTITY PRIMARY KEY,
    RoleName NVARCHAR(50) UNIQUE NOT NULL
);

/* =========================================================
   2. USERS
========================================================= */
CREATE TABLE Users (
    UserID INT IDENTITY PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100),
    Email NVARCHAR(100),
    Phone NVARCHAR(20),
    RoleID INT NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_Users_Roles
        FOREIGN KEY (RoleID) REFERENCES Roles(RoleID)
);

/* =========================================================
   3. WAREHOUSES
========================================================= */
CREATE TABLE Warehouses (
    WarehouseID INT IDENTITY PRIMARY KEY,
    WarehouseName NVARCHAR(100) NOT NULL,
    Address NVARCHAR(255),
    CreatedAt DATETIME DEFAULT GETDATE()
);

/* =========================================================
   4. USER - WAREHOUSE
========================================================= */
CREATE TABLE UserWarehouses (
    UserWarehouseID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL,
    WarehouseID INT NOT NULL,
    AssignedDate DATETIME DEFAULT GETDATE(),

    CONSTRAINT UQ_User_Warehouse UNIQUE (UserID, WarehouseID),
    CONSTRAINT FK_UserWarehouses_Users
        FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
    CONSTRAINT FK_UserWarehouses_Warehouses
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID) ON DELETE CASCADE
);

/* =========================================================
   5. LOCATIONS
========================================================= */
CREATE TABLE Locations (
    LocationID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    LocationCode NVARCHAR(50) NOT NULL,
    LocationName NVARCHAR(100),
    Rack NVARCHAR(20),

    CONSTRAINT FK_Locations_Warehouses
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID) ON DELETE CASCADE,
    CONSTRAINT UQ_Location_Code UNIQUE (WarehouseID, LocationCode)
);

/* =========================================================
   6. PRODUCT CATEGORIES & PRODUCTS
========================================================= */
CREATE TABLE ProductCategories (
    CategoryID INT IDENTITY PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL
);

CREATE TABLE Products (
    ProductID INT IDENTITY PRIMARY KEY,
    ProductName NVARCHAR(150) NOT NULL,
    CategoryID INT NULL,
    Unit NVARCHAR(20),
    CostPrice DECIMAL(18,2) DEFAULT 0,
    SellingPrice DECIMAL(18,2) DEFAULT 0,

    CONSTRAINT FK_Products_Categories
        FOREIGN KEY (CategoryID) REFERENCES ProductCategories(CategoryID)
);

/* =========================================================
   7. INVENTORY
========================================================= */
CREATE TABLE Inventory (
    InventoryID INT IDENTITY PRIMARY KEY,
    ProductID INT NOT NULL,
    LocationID INT NOT NULL,
    Quantity INT DEFAULT 0,
    LastUpdated DATETIME DEFAULT GETDATE(),

    CONSTRAINT UQ_Product_Location UNIQUE (ProductID, LocationID),
    CONSTRAINT FK_Inventory_Products
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE,
    CONSTRAINT FK_Inventory_Locations
        FOREIGN KEY (LocationID) REFERENCES Locations(LocationID) ON DELETE CASCADE,
    CONSTRAINT CK_Quantity_NonNegative CHECK (Quantity >= 0)
);

/* =========================================================
   8. SUPPLIERS
========================================================= */
CREATE TABLE Suppliers (
    SupplierID INT IDENTITY PRIMARY KEY,
    SupplierName NVARCHAR(150) NOT NULL,
    ContactPerson NVARCHAR(100),
    Email NVARCHAR(100),
    Phone NVARCHAR(20),
    Address NVARCHAR(255)
);

/* =========================================================
   9. PURCHASE ORDERS
========================================================= */
CREATE TABLE PurchaseOrders (
    PurchaseOrderID INT IDENTITY PRIMARY KEY,
    SupplierID INT NOT NULL,
    WarehouseID INT NOT NULL,
    CreatedBy INT NOT NULL,
    Status NVARCHAR(30) DEFAULT 'Pending',
    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_PurchaseOrders_Suppliers
        FOREIGN KEY (SupplierID) REFERENCES Suppliers(SupplierID),
    CONSTRAINT FK_PurchaseOrders_Warehouses
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_PurchaseOrders_Users
        FOREIGN KEY (CreatedBy) REFERENCES Users(UserID)
);

CREATE TABLE PurchaseOrderDetails (
    PurchaseOrderDetailID INT IDENTITY PRIMARY KEY,
    PurchaseOrderID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    TotalPrice AS (Quantity * UnitPrice) PERSISTED,

    CONSTRAINT FK_POD_PurchaseOrders
        FOREIGN KEY (PurchaseOrderID) REFERENCES PurchaseOrders(PurchaseOrderID) ON DELETE CASCADE,
    CONSTRAINT FK_POD_Products
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
);

/* =========================================================
   10. SALES ORDERS
========================================================= */
CREATE TABLE SalesOrders (
    SalesOrderID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    CustomerName NVARCHAR(150),
    CustomerPhone NVARCHAR(20),
    CustomerAddress NVARCHAR(255),
    CreatedBy INT NOT NULL,
    Status NVARCHAR(30) DEFAULT 'Pending',
    TotalAmount DECIMAL(18,2) DEFAULT 0,
    Notes NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_SalesOrders_Warehouses
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_SalesOrders_Users
        FOREIGN KEY (CreatedBy) REFERENCES Users(UserID)
);

CREATE TABLE SalesOrderDetails (
    SalesOrderDetailID INT IDENTITY PRIMARY KEY,
    SalesOrderID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    TotalPrice AS (Quantity * UnitPrice) PERSISTED,

    CONSTRAINT FK_SOD_SalesOrders
        FOREIGN KEY (SalesOrderID) REFERENCES SalesOrders(SalesOrderID) ON DELETE CASCADE,
    CONSTRAINT FK_SOD_Products
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
);

/* =========================================================
   11. TRANSFER REQUESTS
========================================================= */
CREATE TABLE TransferRequests (
    TransferID INT IDENTITY PRIMARY KEY,
    FromWarehouseID INT NOT NULL,
    ToWarehouseID INT NOT NULL,
    TransferType NVARCHAR(30) NOT NULL DEFAULT 'Warehouse',
    RequestedBy INT NOT NULL,
    ApprovedBy INT NULL,
    RequestedDate DATETIME DEFAULT GETDATE(),
    ApprovedDate DATETIME NULL,
    Status NVARCHAR(30) DEFAULT 'Pending',
    Reason NVARCHAR(500),

    CONSTRAINT FK_TR_FromWarehouse
        FOREIGN KEY (FromWarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_TR_ToWarehouse
        FOREIGN KEY (ToWarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_TR_RequestedBy
        FOREIGN KEY (RequestedBy) REFERENCES Users(UserID),
    CONSTRAINT FK_TR_ApprovedBy
        FOREIGN KEY (ApprovedBy) REFERENCES Users(UserID),
    CONSTRAINT CK_Different_Warehouses CHECK (FromWarehouseID <> ToWarehouseID)
);

CREATE TABLE TransferDetails (
    TransferDetailID INT IDENTITY PRIMARY KEY,
    TransferID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,

    CONSTRAINT FK_TransferDetails_Transfer
        FOREIGN KEY (TransferID) REFERENCES TransferRequests(TransferID) ON DELETE CASCADE,
    CONSTRAINT FK_TransferDetails_Product
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
);

/* =========================================================
   12. STOCK IN RECEIPTS
========================================================= */
CREATE TABLE StockInReceipts (
    StockInID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    ReceivedBy INT NOT NULL,
    ReceivedDate DATETIME DEFAULT GETDATE(),
    Reason NVARCHAR(30),
    PurchaseOrderID INT NULL,
    TransferID INT NULL,
    TotalAmount DECIMAL(18,2) DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_StockIn_Warehouse
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_StockIn_ReceivedBy
        FOREIGN KEY (ReceivedBy) REFERENCES Users(UserID),
    CONSTRAINT FK_StockIn_PurchaseOrder
        FOREIGN KEY (PurchaseOrderID) REFERENCES PurchaseOrders(PurchaseOrderID),
    CONSTRAINT FK_StockIn_Transfer
        FOREIGN KEY (TransferID) REFERENCES TransferRequests(TransferID)
);

CREATE TABLE StockInDetails (
    StockInDetailID INT IDENTITY PRIMARY KEY,
    StockInID INT NOT NULL,
    ProductID INT NOT NULL,
    LocationID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    TotalPrice AS (Quantity * UnitPrice) PERSISTED,

    CONSTRAINT FK_SID_StockIn
        FOREIGN KEY (StockInID) REFERENCES StockInReceipts(StockInID) ON DELETE CASCADE,
    CONSTRAINT FK_SID_Product
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
    CONSTRAINT FK_SID_Location
        FOREIGN KEY (LocationID) REFERENCES Locations(LocationID)
);

/* =========================================================
   13. STOCK OUT RECEIPTS
========================================================= */
CREATE TABLE StockOutReceipts (
    StockOutID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    IssuedBy INT NOT NULL,
    IssuedDate DATETIME DEFAULT GETDATE(),
    Reason NVARCHAR(30),
    SalesOrderID INT NULL,
    TransferID INT NULL,
    TotalAmount DECIMAL(18,2) DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_StockOut_Warehouse
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_StockOut_IssuedBy
        FOREIGN KEY (IssuedBy) REFERENCES Users(UserID),
    CONSTRAINT FK_StockOut_SalesOrder
        FOREIGN KEY (SalesOrderID) REFERENCES SalesOrders(SalesOrderID),
    CONSTRAINT FK_StockOut_Transfer
        FOREIGN KEY (TransferID) REFERENCES TransferRequests(TransferID)
);

CREATE TABLE StockOutDetails (
    StockOutDetailID INT IDENTITY PRIMARY KEY,
    StockOutID INT NOT NULL,
    ProductID INT NOT NULL,
    LocationID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    TotalPrice AS (Quantity * UnitPrice) PERSISTED,

    CONSTRAINT FK_SOD_StockOut
        FOREIGN KEY (StockOutID) REFERENCES StockOutReceipts(StockOutID) ON DELETE CASCADE,
    CONSTRAINT FK_SOD_Product
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
    CONSTRAINT FK_SOD_Location
        FOREIGN KEY (LocationID) REFERENCES Locations(LocationID)
);

/* =========================================================
   14. ACTIVITY LOGS
========================================================= */
CREATE TABLE ActivityLogs (
    LogID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL,
    Action NVARCHAR(100) NOT NULL,
    TableName NVARCHAR(50),
    RecordID INT,
    Description NVARCHAR(500),
    IPAddress NVARCHAR(50),
    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_ActivityLogs_Users
        FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

/* =========================================================
   INDEXES FOR PERFORMANCE
========================================================= */
CREATE INDEX IX_Inventory_LocationID ON Inventory(LocationID);
CREATE INDEX IX_Inventory_ProductID ON Inventory(ProductID);
CREATE INDEX IX_StockInDetails_StockInID ON StockInDetails(StockInID);
CREATE INDEX IX_StockOutDetails_StockOutID ON StockOutDetails(StockOutID);
CREATE INDEX IX_StockInReceipts_WarehouseID_Date ON StockInReceipts(WarehouseID, ReceivedDate);
CREATE INDEX IX_StockOutReceipts_WarehouseID_Date ON StockOutReceipts(WarehouseID, IssuedDate);
CREATE INDEX IX_UserWarehouses_UserID ON UserWarehouses(UserID);
CREATE INDEX IX_UserWarehouses_WarehouseID ON UserWarehouses(WarehouseID);

/* =========================================================
   SAMPLE DATA FOR EWMS DATABASE
   Run this AFTER creating the schema
========================================================= */

/* =========================================================
   1. ROLES
========================================================= */
INSERT INTO Roles (RoleName) VALUES
('Admin'),
('Warehouse Manager'),
('Sales Staff'),
('Purchasing Staff'),
('Inventory Staff');

/* =========================================================
   2. USERS (PASSWORD = 123456)
========================================================= */
-- Admin
INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, RoleID) VALUES
('admin', '123456', 'System Administrator', 'admin@ewms.com', '0900000000', 1);

-- Warehouse Managers
INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, RoleID) VALUES
('wm_hanoi', '123456', 'Nguyen Van A', 'wm.hanoi@ewms.com', '0901111111', 2),
('wm_hcm', '123456', 'Tran Thi B', 'wm.hcm@ewms.com', '0902222222', 2),
('wm_danang', '123456', 'Le Van C', 'wm.danang@ewms.com', '0903333333', 2);

-- Hanoi Staff
INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, RoleID) VALUES
('hn_sales_01', '123456', 'Pham Van D', 'sales.hn@ewms.com', '0904444444', 3),
('hn_purchase_01', '123456', 'Hoang Thi E', 'purchase.hn@ewms.com', '0905555555', 4),
('hn_inventory_01', '123456', 'Nguyen Van F', 'inventory.hn@ewms.com', '0906666666', 5);

-- HCM Staff
INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, RoleID) VALUES
('hcm_sales_01', '123456', 'Vo Van G', 'sales.hcm@ewms.com', '0907777777', 3),
('hcm_purchase_01', '123456', 'Dang Thi H', 'purchase.hcm@ewms.com', '0908888888', 4),
('hcm_inventory_01', '123456', 'Bui Van I', 'inventory.hcm@ewms.com', '0909999999', 5);

-- Da Nang Staff
INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, RoleID) VALUES
('dn_sales_01', '123456', 'Mai Van K', 'sales.dn@ewms.com', '0911111111', 3),
('dn_purchase_01', '123456', 'Duong Thi L', 'purchase.dn@ewms.com', '0912222222', 4),
('dn_inventory_01', '123456', 'Ngo Van M', 'inventory.dn@ewms.com', '0913333333', 5);

/* =========================================================
   3. WAREHOUSES
========================================================= */
INSERT INTO Warehouses (WarehouseName, Address) VALUES
('Kho Hà Nội', '123 Hai Bà Trưng, Hoàn Kiếm, Hà Nội'),
('Kho TP.HCM', '456 Nguyễn Huệ, Quận 1, TP.HCM'),
('Kho Đà Nẵng', '789 Bạch Đằng, Hải Châu, Đà Nẵng');

/* =========================================================
   4. USER - WAREHOUSE MAPPING
========================================================= */
-- Admin có quyền tất cả warehouses
INSERT INTO UserWarehouses (UserID, WarehouseID) VALUES
(1, 1), (1, 2), (1, 3);

-- Warehouse Managers
INSERT INTO UserWarehouses (UserID, WarehouseID) VALUES
(2, 1), -- wm_hanoi -> Hanoi
(3, 2), -- wm_hcm -> HCM
(4, 3); -- wm_danang -> Da Nang

-- Hanoi Staff -> Warehouse 1
INSERT INTO UserWarehouses (UserID, WarehouseID) VALUES
(5, 1), (6, 1), (7, 1);

-- HCM Staff -> Warehouse 2
INSERT INTO UserWarehouses (UserID, WarehouseID) VALUES
(8, 2), (9, 2), (10, 2);

-- Da Nang Staff -> Warehouse 3
INSERT INTO UserWarehouses (UserID, WarehouseID) VALUES
(11, 3), (12, 3), (13, 3);

/* =========================================================
   5. LOCATIONS (3 per warehouse)
========================================================= */
INSERT INTO Locations (WarehouseID, LocationCode, LocationName, Rack) VALUES
-- Hanoi
(1, 'HN-A1', 'Khu A - Giá 1', 'A1'),
(1, 'HN-A2', 'Khu A - Giá 2', 'A2'),
(1, 'HN-B1', 'Khu B - Giá 1', 'B1'),
-- HCM
(2, 'HCM-A1', 'Khu A - Giá 1', 'A1'),
(2, 'HCM-A2', 'Khu A - Giá 2', 'A2'),
(2, 'HCM-B1', 'Khu B - Giá 1', 'B1'),
-- Da Nang
(3, 'DN-A1', 'Khu A - Giá 1', 'A1'),
(3, 'DN-A2', 'Khu A - Giá 2', 'A2'),
(3, 'DN-B1', 'Khu B - Giá 1', 'B1');

/* =========================================================
   6. PRODUCT CATEGORIES
========================================================= */
INSERT INTO ProductCategories (CategoryName) VALUES
('Điện thoại'),
('Laptop'),
('Phụ kiện');

/* =========================================================
   7. PRODUCTS
========================================================= */
INSERT INTO Products (ProductName, CategoryID, Unit, CostPrice, SellingPrice) VALUES
-- Smartphones
('iPhone 15 Pro 256GB', 1, 'Cái', 25000000, 30000000),
('iPhone 14 128GB', 1, 'Cái', 18000000, 22000000),
('Samsung Galaxy S24', 1, 'Cái', 20000000, 24000000),
('Samsung Galaxy S23', 1, 'Cái', 15000000, 18000000),
('Xiaomi 14', 1, 'Cái', 12000000, 15000000),
-- Laptops
('MacBook Pro M3 14"', 2, 'Cái', 45000000, 52000000),
('MacBook Air M2', 2, 'Cái', 28000000, 33000000),
('Dell XPS 13', 2, 'Cái', 30000000, 35000000),
('HP Spectre x360', 2, 'Cái', 25000000, 29000000),
('Asus ZenBook 14', 2, 'Cái', 22000000, 26000000),
-- Accessories
('Chuột Logitech MX Master 3', 3, 'Cái', 1800000, 2500000),
('Bàn phím cơ RGB', 3, 'Cái', 1200000, 1800000),
('Cáp USB-C 2m', 3, 'Cái', 150000, 300000),
('Sạc không dây 65W', 3, 'Cái', 800000, 1200000),
('Tai nghe Bluetooth', 3, 'Cái', 2000000, 2800000),
('Pin dự phòng 20000mAh', 3, 'Cái', 500000, 800000),
('Balo laptop', 3, 'Cái', 400000, 700000),
('Loa Bluetooth', 3, 'Cái', 1500000, 2200000);

/* =========================================================
   8. INITIAL INVENTORY
========================================================= */
INSERT INTO Inventory (ProductID, LocationID, Quantity) VALUES
-- Hanoi (Locations 1-3)
(1, 1, 50),  -- iPhone 15 Pro
(2, 1, 40),  -- iPhone 14
(3, 2, 60),  -- Samsung S24
(6, 2, 15),  -- MacBook Pro M3
(11, 3, 100), -- Chuột Logitech
(13, 3, 200), -- Cáp USB-C
-- HCM (Locations 4-6)
(1, 4, 45),
(3, 4, 55),
(6, 5, 20),
(7, 5, 30),
(12, 6, 150),
-- Da Nang (Locations 7-9)
(2, 7, 35),
(5, 7, 25),
(8, 8, 18),
(9, 8, 22),
(14, 9, 250);

/* =========================================================
   9. SUPPLIERS
========================================================= */
INSERT INTO Suppliers (SupplierName, ContactPerson, Email, Phone, Address) VALUES
('Apple Vietnam', 'Nguyen Anh', 'contact@apple.vn', '0281234567', 'TP.HCM'),
('Samsung Vietnam', 'Tran Binh', 'info@samsung.vn', '0241234567', 'Hà Nội'),
('Nhà phân phối IT', 'Le Chi', 'sales@itdist.vn', '0236123456', 'Đà Nẵng'),
('Công ty Phụ kiện', 'Pham Dung', 'order@accessory.vn', '0281234999', 'TP.HCM');

/* =========================================================
   10. PURCHASE ORDERS
========================================================= */
INSERT INTO PurchaseOrders (SupplierID, WarehouseID, CreatedBy, Status) VALUES
(1, 1, 6, 'Received'),  -- Apple -> Hanoi
(2, 2, 9, 'Received'),  -- Samsung -> HCM
(3, 3, 12, 'Pending'),  -- IT Dist -> Da Nang
(4, 1, 6, 'Approved'); -- Accessory -> Hanoi

INSERT INTO PurchaseOrderDetails (PurchaseOrderID, ProductID, Quantity, UnitPrice) VALUES
-- PO1: Apple products to Hanoi
(1, 1, 20, 25000000), -- iPhone 15 Pro x20
(1, 6, 10, 45000000), -- MacBook Pro M3 x10
-- PO2: Samsung to HCM
(2, 3, 24, 20000000), -- Samsung S24 x24
(2, 4, 15, 15000000), -- Samsung S23 x15
-- PO3: Laptops to Da Nang
(3, 8, 12, 30000000), -- Dell XPS x12
(3, 10, 8, 22000000), -- Asus ZenBook x8
-- PO4: Accessories to Hanoi
(4, 13, 500, 150000), -- USB-C Cable x500
(4, 14, 100, 800000); -- Wireless Charger x100

/* =========================================================
   11. SALES ORDERS
========================================================= */
INSERT INTO SalesOrders (WarehouseID, CustomerName, CustomerPhone, CustomerAddress, CreatedBy, Status, TotalAmount) VALUES
(1, 'Công ty ABC', '0912345678', 'Hà Nội', 5, 'Completed', 90000000),
(2, 'Công ty XYZ', '0923456789', 'TP.HCM', 8, 'Completed', 72000000),
(1, 'Khách lẻ Nguyễn Văn A', '0934567890', 'Hà Nội', 5, 'Pending', 52000000),
(3, 'Công ty DEF', '0945678901', 'Đà Nẵng', 11, 'Completed', 35000000);

INSERT INTO SalesOrderDetails (SalesOrderID, ProductID, Quantity, UnitPrice) VALUES
-- SO1: Hanoi - Completed
(1, 1, 3, 30000000), -- iPhone 15 Pro x3
-- SO2: HCM - Completed  
(2, 3, 3, 24000000), -- Samsung S24 x3
-- SO3: Hanoi - Pending
(3, 6, 1, 52000000), -- MacBook Pro M3 x1
-- SO4: Da Nang - Completed
(4, 8, 1, 35000000); -- Dell XPS x1

/* =========================================================
   12. TRANSFER REQUESTS
========================================================= */
INSERT INTO TransferRequests (FromWarehouseID, ToWarehouseID, TransferType, RequestedBy, ApprovedBy, RequestedDate, ApprovedDate, Status, Reason) VALUES
(1, 2, 'Warehouse', 2, 1, '2025-01-20', '2025-01-21', 'Approved', 'Cân bằng tồn kho'),
(2, 3, 'Warehouse', 3, 1, '2025-01-23', NULL, 'Pending', 'Thiếu hàng tại Đà Nẵng'),
(1, 3, 'Warehouse', 2, 1, '2025-01-24', '2025-01-24', 'Completed', 'Hỗ trợ chi nhánh');

INSERT INTO TransferDetails (TransferID, ProductID, Quantity) VALUES
(1, 11, 20),  -- Transfer Chuột Logitech x20
(2, 13, 50),  -- Transfer USB-C Cable x50
(3, 2, 10);   -- Transfer iPhone 14 x10

/* =========================================================
   13. STOCK IN RECEIPTS
========================================================= */
INSERT INTO StockInReceipts (WarehouseID, ReceivedBy, ReceivedDate, Reason, PurchaseOrderID, TotalAmount) VALUES
(1, 7, '2025-01-15', 'Purchase', 1, 950000000),
(2, 10, '2025-01-17', 'Purchase', 2, 705000000),
(1, 7, '2025-01-22', 'Purchase', 4, 155000000);

INSERT INTO StockInDetails (StockInID, ProductID, LocationID, Quantity, UnitPrice) VALUES
-- SI1: Hanoi - PO1
(1, 1, 1, 20, 25000000), -- iPhone 15 Pro x20
(1, 6, 2, 10, 45000000), -- MacBook Pro M3 x10
-- SI2: HCM - PO2
(2, 3, 4, 24, 20000000), -- Samsung S24 x24
(2, 4, 4, 15, 15000000), -- Samsung S23 x15
-- SI3: Hanoi - PO4 (Accessories)
(3, 13, 3, 500, 150000), -- USB-C Cable x500
(3, 14, 3, 100, 800000); -- Wireless Charger x100

/* =========================================================
   14. STOCK OUT RECEIPTS
========================================================= */
INSERT INTO StockOutReceipts (WarehouseID, IssuedBy, IssuedDate, Reason, SalesOrderID, TotalAmount) VALUES
(1, 5, '2025-01-18', 'Sale', 1, 90000000),
(2, 8, '2025-01-19', 'Sale', 2, 72000000),
(3, 11, '2025-01-21', 'Sale', 4, 35000000),
(1, 7, '2025-01-24', 'Transfer', NULL, 0); -- Transfer to Da Nang

INSERT INTO StockOutDetails (StockOutID, ProductID, LocationID, Quantity, UnitPrice) VALUES
-- SO1: Sale from Hanoi
(1, 1, 1, 3, 30000000), -- iPhone 15 Pro x3
-- SO2: Sale from HCM
(2, 3, 4, 3, 24000000), -- Samsung S24 x3
-- SO3: Sale from Da Nang
(3, 8, 8, 1, 35000000), -- Dell XPS x1
-- SO4: Transfer from Hanoi
(4, 2, 1, 10, 0);       -- iPhone 14 x10 (transfer, không tính giá bán)

/* =========================================================
   15. ACTIVITY LOGS (SAMPLE)
========================================================= */
INSERT INTO ActivityLogs (UserID, Action, TableName, RecordID, Description, IPAddress) VALUES
(6, 'Created', 'PurchaseOrders', 1, 'Tạo đơn mua hàng từ Apple Vietnam', '192.168.1.100'),
(7, 'Received', 'StockInReceipts', 1, 'Nhập kho 20 iPhone 15 Pro, 10 MacBook Pro M3', '192.168.1.101'),
(5, 'Created', 'SalesOrders', 1, 'Tạo đơn bán hàng cho Công ty ABC', '192.168.1.102'),
(5, 'Issued', 'StockOutReceipts', 1, 'Xuất kho 3 iPhone 15 Pro', '192.168.1.102'),
(2, 'Created', 'TransferRequests', 1, 'Yêu cầu chuyển 20 chuột Logitech từ HN sang HCM', '192.168.1.103'),
(1, 'Approved', 'TransferRequests', 1, 'Phê duyệt yêu cầu chuyển kho TR001', '192.168.1.1');





