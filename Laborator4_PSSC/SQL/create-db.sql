IF DB_ID(N'ShoppingCart') IS NULL
    CREATE DATABASE ShoppingCart;
GO

USE ShoppingCart;
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Products
(
    Id           INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Code         NVARCHAR(20)       NOT NULL CONSTRAINT UQ_Products_Code UNIQUE,
    Name         NVARCHAR(100)      NOT NULL,
    Price        DECIMAL(10, 2)     NOT NULL CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    QuantityType NVARCHAR(10)       NOT NULL CONSTRAINT CK_Products_QuantityType CHECK (QuantityType IN (N'Unit', N'Kg'))
);
GO

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
CREATE TABLE dbo.Customers
(
    Id   INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Code NVARCHAR(20)       NOT NULL CONSTRAINT UQ_Customers_Code UNIQUE,
    Name NVARCHAR(100)      NOT NULL
);
GO

-- "Order" is a reserved word in SQL, so the name must be bracketed.
IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
CREATE TABLE dbo.[Order]
(
    Id              INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Order PRIMARY KEY,
    [Date]          DATETIME2          NOT NULL,
    DeliveryAddress NVARCHAR(200)      NOT NULL,
    CustomerId      INT                NOT NULL CONSTRAINT FK_Order_Customers REFERENCES dbo.Customers (Id)
);
GO

IF OBJECT_ID(N'dbo.OrderItem', N'U') IS NULL
CREATE TABLE dbo.OrderItem
(
    Id        INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_OrderItem PRIMARY KEY,
    OrderId   INT                NOT NULL CONSTRAINT FK_OrderItem_Order REFERENCES dbo.[Order] (Id),
    ProductId INT                NOT NULL CONSTRAINT FK_OrderItem_Products REFERENCES dbo.Products (Id),
    Quantity  DECIMAL(10, 3)     NOT NULL CONSTRAINT CK_OrderItem_Quantity CHECK (Quantity > 0),
    CONSTRAINT UQ_OrderItem_Order_Product UNIQUE (OrderId, ProductId)
);
GO
