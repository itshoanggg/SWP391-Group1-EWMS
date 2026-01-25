create database EWMS
go 
use EWMS

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
    RoleID INT NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_Users_Roles
        FOREIGN KEY (RoleID) REFERENCES Roles(RoleID)
);

/* =========================================================
   3. WAREHOUSES
========================================================= */
CREATE TABLE Warehouses (
    WarehouseID INT IDENTITY PRIMARY KEY,
    WarehouseName NVARCHAR(100) NOT NULL
);

/* =========================================================
   4. USER - WAREHOUSE
========================================================= */
CREATE TABLE UserWarehouses (
    UserWarehouseID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL,
    WarehouseID INT NOT NULL,
    IsPrimary BIT DEFAULT 1,
    AssignedDate DATETIME DEFAULT GETDATE(),

    CONSTRAINT UQ_User_Warehouse UNIQUE (UserID, WarehouseID),
    CONSTRAINT FK_UserWarehouses_Users
        FOREIGN KEY (UserID) REFERENCES Users(UserID),
    CONSTRAINT FK_UserWarehouses_Warehouses
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID)
);

/* =========================================================
   5. LOCATIONS (BIN / POSITION)
========================================================= */
CREATE TABLE Locations (
    LocationID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    LocationCode NVARCHAR(50) NOT NULL,
    IsActive BIT DEFAULT 1,

    CONSTRAINT FK_Locations_Warehouses
        FOREIGN KEY (WarehouseID) REFERENCES Warehouses(WarehouseID)
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
    IsActive BIT DEFAULT 1,

    CONSTRAINT FK_Products_Categories
        FOREIGN KEY (CategoryID) REFERENCES ProductCategories(CategoryID)
);

/* =========================================================
   7. INVENTORY (PER LOCATION)
========================================================= */
CREATE TABLE Inventory (
    ProductID INT NOT NULL,
    LocationID INT NOT NULL,
    Quantity INT DEFAULT 0,

    CONSTRAINT PK_Inventory PRIMARY KEY (ProductID, LocationID),
    CONSTRAINT FK_Inventory_Products
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
    CONSTRAINT FK_Inventory_Locations
        FOREIGN KEY (LocationID) REFERENCES Locations(LocationID)
);

/* =========================================================
   8. SUPPLIERS
========================================================= */
CREATE TABLE Suppliers (
    SupplierID INT IDENTITY PRIMARY KEY,
    SupplierName NVARCHAR(150) NOT NULL
);

/* =========================================================
   9. PURCHASE ORDERS
========================================================= */
CREATE TABLE PurchaseOrders (
    PurchaseOrderID INT IDENTITY PRIMARY KEY,
    SupplierID INT NOT NULL,
    CreatedBy INT NOT NULL,
    OrderDate DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(30),

    CONSTRAINT FK_PurchaseOrders_Suppliers
        FOREIGN KEY (SupplierID) REFERENCES Suppliers(SupplierID),
    CONSTRAINT FK_PurchaseOrders_Users
        FOREIGN KEY (CreatedBy) REFERENCES Users(UserID)
);

CREATE TABLE PurchaseOrderDetails (
    PurchaseOrderID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2),

    CONSTRAINT PK_PurchaseOrderDetails PRIMARY KEY (PurchaseOrderID, ProductID),
    CONSTRAINT FK_POD_PurchaseOrders
        FOREIGN KEY (PurchaseOrderID) REFERENCES PurchaseOrders(PurchaseOrderID),
    CONSTRAINT FK_POD_Products
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
);

/* =========================================================
   10. SALES ORDERS
========================================================= */
CREATE TABLE SalesOrders (
    SalesOrderID INT IDENTITY PRIMARY KEY,
    CreatedBy INT NOT NULL,
    OrderDate DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(30),

    CONSTRAINT FK_SalesOrders_Users
        FOREIGN KEY (CreatedBy) REFERENCES Users(UserID)
);

CREATE TABLE SalesOrderDetails (
    SalesOrderID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2),

    CONSTRAINT PK_SalesOrderDetails PRIMARY KEY (SalesOrderID, ProductID),
    CONSTRAINT FK_SOD_SalesOrders
        FOREIGN KEY (SalesOrderID) REFERENCES SalesOrders(SalesOrderID),
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
    TransferType NVARCHAR(30) NOT NULL,
    RequestedBy INT NOT NULL,
    ApprovedBy INT NULL,
    RequestedDate DATETIME DEFAULT GETDATE(),
    ApprovedDate DATETIME NULL,
    Status NVARCHAR(30),

    CONSTRAINT FK_TR_FromWarehouse
        FOREIGN KEY (FromWarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_TR_ToWarehouse
        FOREIGN KEY (ToWarehouseID) REFERENCES Warehouses(WarehouseID),
    CONSTRAINT FK_TR_RequestedBy
        FOREIGN KEY (RequestedBy) REFERENCES Users(UserID),
    CONSTRAINT FK_TR_ApprovedBy
        FOREIGN KEY (ApprovedBy) REFERENCES Users(UserID)
);

CREATE TABLE TransferDetails (
    TransferDetailID INT IDENTITY PRIMARY KEY,
    TransferID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,

    CONSTRAINT FK_TransferDetails_Transfer
        FOREIGN KEY (TransferID) REFERENCES TransferRequests(TransferID),
    CONSTRAINT FK_TransferDetails_Product
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
);

/* =========================================================
   12. STOCK OUT
========================================================= */
CREATE TABLE StockOutReceipts (
    StockOutID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    IssuedBy INT NOT NULL,
    IssuedDate DATETIME DEFAULT GETDATE(),
    Reason NVARCHAR(30),
    SalesOrderID INT NULL,
    TransferID INT NULL,

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
    StockOutID INT NOT NULL,
    ProductID INT NOT NULL,
    LocationID INT NOT NULL,
    Quantity INT NOT NULL,

    CONSTRAINT PK_StockOutDetails PRIMARY KEY (StockOutID, ProductID, LocationID),
    CONSTRAINT FK_SOD_StockOut
        FOREIGN KEY (StockOutID) REFERENCES StockOutReceipts(StockOutID),
    CONSTRAINT FK_SOD_Product
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
    CONSTRAINT FK_SOD_Location
        FOREIGN KEY (LocationID) REFERENCES Locations(LocationID)
);

/* =========================================================
   13. STOCK IN
========================================================= */
CREATE TABLE StockInReceipts (
    StockInID INT IDENTITY PRIMARY KEY,
    WarehouseID INT NOT NULL,
    ReceivedBy INT NOT NULL,
    ReceivedDate DATETIME DEFAULT GETDATE(),
    Reason NVARCHAR(30),
    PurchaseOrderID INT NULL,
    TransferID INT NULL,

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
    StockInID INT NOT NULL,
    ProductID INT NOT NULL,
    LocationID INT NOT NULL,
    Quantity INT NOT NULL,

    CONSTRAINT PK_StockInDetails PRIMARY KEY (StockInID, ProductID, LocationID),
    CONSTRAINT FK_SID_StockIn
        FOREIGN KEY (StockInID) REFERENCES StockInReceipts(StockInID),
    CONSTRAINT FK_SID_Product
        FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
    CONSTRAINT FK_SID_Location
        FOREIGN KEY (LocationID) REFERENCES Locations(LocationID)
);



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
INSERT INTO Users (Username, PasswordHash, FullName, RoleID) VALUES
('admin', '123456', 'System Administrator', 1);

-- Warehouse Managers (global)
INSERT INTO Users (Username, PasswordHash, FullName, RoleID) VALUES
('wm_01', '123456', 'John Warehouse Manager', 2),
('wm_02', '123456', 'Sarah Warehouse Manager', 2);


-- ===== STAFF =====
-- Hanoi
INSERT INTO Users VALUES
('hn_sales_01','123456','Hanoi Sales Staff 01',3,1,GETDATE()),
('hn_pur_01','123456','Hanoi Purchasing Staff 01',4,1,GETDATE()),
('hn_inv_01','123456','Hanoi Inventory Staff 01',5,1,GETDATE());

-- HCM
INSERT INTO Users VALUES
('hcm_sales_01','123456','HCM Sales Staff 01',3,1,GETDATE()),
('hcm_pur_01','123456','HCM Purchasing Staff 01',4,1,GETDATE()),
('hcm_inv_01','123456','HCM Inventory Staff 01',5,1,GETDATE());

-- Da Nang
INSERT INTO Users VALUES
('dn_sales_01','123456','Da Nang Sales Staff 01',3,1,GETDATE()),
('dn_pur_01','123456','Da Nang Purchasing Staff 01',4,1,GETDATE()),
('dn_inv_01','123456','Da Nang Inventory Staff 01',5,1,GETDATE());

-- Can Tho
INSERT INTO Users VALUES
('ct_sales_01','123456','Can Tho Sales Staff 01',3,1,GETDATE()),
('ct_pur_01','123456','Can Tho Purchasing Staff 01',4,1,GETDATE()),
('ct_inv_01','123456','Can Tho Inventory Staff 01',5,1,GETDATE());

-- Hai Phong
INSERT INTO Users VALUES
('hp_sales_01','123456','Hai Phong Sales Staff 01',3,1,GETDATE()),
('hp_pur_01','123456','Hai Phong Purchasing Staff 01',4,1,GETDATE()),
('hp_inv_01','123456','Hai Phong Inventory Staff 01',5,1,GETDATE());


/* =========================================================
   3. WAREHOUSES
========================================================= */
INSERT INTO Warehouses (WarehouseName) VALUES
('Hanoi Central Warehouse'),
('Ho Chi Minh City Warehouse'),
('Da Nang Warehouse'),
('Can Tho Warehouse'),
('Hai Phong Warehouse');


/* =========================================================
   4. USER - WAREHOUSE MAPPING
========================================================= */
-- Admin & Managers -> all warehouses
INSERT INTO UserWarehouses (UserID, WarehouseID)
SELECT 1, WarehouseID FROM Warehouses;

INSERT INTO UserWarehouses (UserID, WarehouseID)
SELECT 2, WarehouseID FROM Warehouses;

INSERT INTO UserWarehouses (UserID, WarehouseID)
SELECT 3, WarehouseID FROM Warehouses;

-- Hanoi (WarehouseID = 1)
INSERT INTO UserWarehouses VALUES
(4,1,1,GETDATE()),
(5,1,1,GETDATE()),
(6,1,1,GETDATE());

-- HCM (2)
INSERT INTO UserWarehouses VALUES
(7,2,1,GETDATE()),
(8,2,1,GETDATE()),
(9,2,1,GETDATE());

-- Da Nang (3)
INSERT INTO UserWarehouses VALUES
(10,3,1,GETDATE()),
(11,3,1,GETDATE()),
(12,3,1,GETDATE());

-- Can Tho (4)
INSERT INTO UserWarehouses VALUES
(13,4,1,GETDATE()),
(14,4,1,GETDATE()),
(15,4,1,GETDATE());

-- Hai Phong (5)
INSERT INTO UserWarehouses VALUES
(16,5,1,GETDATE()),
(17,5,1,GETDATE()),
(18,5,1,GETDATE());


/* =========================================================
   5. LOCATIONS (3 PER WAREHOUSE)
========================================================= */
INSERT INTO Locations (WarehouseID, LocationCode) VALUES
(1,'HN-A1'),(1,'HN-A2'),(1,'HN-B1'),
(2,'HCM-A1'),(2,'HCM-A2'),(2,'HCM-B1'),
(3,'DN-A1'),(3,'DN-A2'),(3,'DN-B1'),
(4,'CT-A1'),(4,'CT-A2'),(4,'CT-B1'),
(5,'HP-A1'),(5,'HP-A2'),(5,'HP-B1');


/* =========================================================
   6. PRODUCT CATEGORIES & PRODUCTS (MANY)
========================================================= */
INSERT INTO ProductCategories (CategoryName) VALUES
('Smartphone'),
('Laptop'),
('Accessory');

INSERT INTO Products (ProductName, CategoryID, Unit) VALUES
('iPhone 15 Pro',1,'Piece'),
('iPhone 14',1,'Piece'),
('Samsung Galaxy S24',1,'Piece'),
('Samsung Galaxy S23',1,'Piece'),
('Xiaomi 14',1,'Piece'),
('MacBook Pro M3',2,'Piece'),
('MacBook Air M2',2,'Piece'),
('Dell XPS 13',2,'Piece'),
('HP Spectre x360',2,'Piece'),
('Asus ZenBook 14',2,'Piece'),
('Logitech Mouse',3,'Piece'),
('Mechanical Keyboard',3,'Piece'),
('USB-C Cable',3,'Piece'),
('Wireless Charger',3,'Piece'),
('Headphones',3,'Piece'),
('Power Bank',3,'Piece'),
('Laptop Backpack',3,'Piece'),
('Bluetooth Speaker',3,'Piece');


/* =========================================================
   7. INVENTORY (LOTS OF DATA)
========================================================= */
INSERT INTO Inventory VALUES
(1,1,50),(2,1,40),(3,2,60),(4,3,30),(5,3,20),
(6,4,15),(7,4,25),(8,5,10),(9,6,18),(10,6,12),
(11,7,100),(12,8,80),(13,9,200),(14,10,150),(15,11,90),
(16,12,70),(17,13,40),(18,14,35);


/* =========================================================
   8. SUPPLIERS
========================================================= */
INSERT INTO Suppliers (SupplierName) VALUES
('Apple Vietnam'),
('Samsung Vietnam'),
('Global IT Distributor'),
('Asia Accessories Ltd'),
('Tech Wholesale Group');


/* =========================================================
   9. PURCHASE ORDERS (MANY)
========================================================= */
INSERT INTO PurchaseOrders (SupplierID, CreatedBy, Status) VALUES
(1,5,'Approved'),
(2,8,'Approved'),
(3,11,'Approved'),
(4,14,'Approved'),
(5,17,'Pending'),
(1,5,'Pending'),
(2,8,'Pending');

INSERT INTO PurchaseOrderDetails VALUES
(1,1,20,1200),(1,6,5,2500),
(2,3,30,900),(2,11,100,25),
(3,8,10,1800),(3,12,50,70),
(4,13,200,10),
(5,2,25,1000),
(6,4,15,950),
(7,18,40,60);


/* =========================================================
   10. SALES ORDERS (LOTS)
========================================================= */
INSERT INTO SalesOrders (CreatedBy, Status) VALUES
(4,'Completed'),
(7,'Completed'),
(10,'Completed'),
(13,'Completed'),
(16,'Completed'),
(4,'Pending'),
(7,'Pending'),
(10,'Pending');

INSERT INTO SalesOrderDetails VALUES
(1,1,3,1300),(1,11,5,30),
(2,3,2,950),
(3,6,1,2600),
(4,9,1,2000),
(5,15,4,120),
(6,2,2,1100),
(7,5,1,800),
(8,18,3,70);


/* =========================================================
   11. TRANSFER REQUESTS
========================================================= */
INSERT INTO TransferRequests
(FromWarehouseID,ToWarehouseID,TransferType,RequestedBy,ApprovedBy,Status)
VALUES
(1,2,'Warehouse',6,2,'Approved'),
(2,3,'Warehouse',9,2,'Approved'),
(3,4,'Warehouse',12,1,'Approved'),
(4,5,'Warehouse',15,1,'Pending');

INSERT INTO TransferDetails VALUES
(1,1,10),(1,11,20),
(2,3,15),
(3,6,5),
(4,8,7);


/* =========================================================
   12. STOCK IN
========================================================= */
INSERT INTO StockInReceipts
(WarehouseID,ReceivedBy,Reason,PurchaseOrderID)
VALUES
(1,6,'Purchase',1),
(2,9,'Purchase',2),
(3,12,'Purchase',3),
(4,15,'Purchase',4);

INSERT INTO StockInDetails VALUES
(1,1,1,20),(1,6,2,5),
(2,3,4,30),
(3,8,7,10),
(4,13,10,200);


/* =========================================================
   13. STOCK OUT
========================================================= */
INSERT INTO StockOutReceipts
(WarehouseID,IssuedBy,Reason,SalesOrderID)
VALUES
(1,4,'Sale',1),
(2,7,'Sale',2),
(3,10,'Sale',3),
(4,13,'Sale',4);

INSERT INTO StockOutDetails VALUES
(1,1,1,3),
(2,3,4,2),
(3,6,7,1),
(4,9,10,1);


