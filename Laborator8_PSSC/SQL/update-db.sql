-- Extends the database for Lab 8: the invoicing context's tables, in their own schema.
-- The invoicing worker writes here; the shop's tables are not touched. Safe to run several times.

USE ShoppingCart;
GO

IF SCHEMA_ID(N'invoicing') IS NULL
    EXEC (N'CREATE SCHEMA invoicing');
GO

IF OBJECT_ID(N'invoicing.Invoices', N'U') IS NULL
CREATE TABLE invoicing.Invoices
(
    Id             INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    Number         NVARCHAR(30)       NOT NULL CONSTRAINT UQ_Invoices_Number UNIQUE,
    -- One invoice per order: a repeated "order placed" event cannot create a second one.
    OrderId        INT                NOT NULL CONSTRAINT UQ_Invoices_OrderId UNIQUE,
    CustomerCode   NVARCHAR(20)       NOT NULL,
    CustomerName   NVARCHAR(100)      NOT NULL,
    BillingAddress NVARCHAR(200)      NOT NULL,
    IssuedAt       DATETIME2          NOT NULL,
    NetAmount      DECIMAL(12, 2)     NOT NULL,
    VatAmount      DECIMAL(12, 2)     NOT NULL,
    Total          DECIMAL(12, 2)     NOT NULL,
    CONSTRAINT CK_Invoices_Total CHECK (Total = NetAmount + VatAmount)
);
GO

IF OBJECT_ID(N'invoicing.InvoiceLines', N'U') IS NULL
CREATE TABLE invoicing.InvoiceLines
(
    Id          INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_InvoiceLines PRIMARY KEY,
    InvoiceId   INT                NOT NULL CONSTRAINT FK_InvoiceLines_Invoices REFERENCES invoicing.Invoices (Id),
    ProductCode NVARCHAR(20)       NOT NULL,
    ProductName NVARCHAR(100)      NOT NULL,
    Quantity    DECIMAL(10, 3)     NOT NULL,
    UnitPrice   DECIMAL(10, 2)     NOT NULL,
    NetAmount   DECIMAL(12, 2)     NOT NULL,
    VatAmount   DECIMAL(12, 2)     NOT NULL,
    Total       DECIMAL(12, 2)     NOT NULL
);
GO
