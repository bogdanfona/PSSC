-- Extends the Lab 5 database (Laborator5_PSSC/SQL/update-db.sql) for Lab 6.
-- A shopping cart is an Order row: it can take more products while Unpaid, and none once Paid.
-- Safe to run several times.

USE ShoppingCart;
GO

IF COL_LENGTH(N'dbo.[Order]', N'Status') IS NULL
    ALTER TABLE dbo.[Order]
        ADD Status NVARCHAR(10) NOT NULL
            CONSTRAINT DF_Order_Status DEFAULT N'Unpaid'
            CONSTRAINT CK_Order_Status CHECK (Status IN (N'Unpaid', N'Paid'));
GO

IF COL_LENGTH(N'dbo.[Order]', N'PaidAt') IS NULL
    ALTER TABLE dbo.[Order] ADD PaidAt DATETIME2 NULL;
GO

-- A paid cart must say when it was paid; an unpaid one must not.
IF OBJECT_ID(N'dbo.CK_Order_PaidAt', N'C') IS NULL
    ALTER TABLE dbo.[Order]
        ADD CONSTRAINT CK_Order_PaidAt CHECK (
            (Status = N'Paid' AND PaidAt IS NOT NULL) OR (Status = N'Unpaid' AND PaidAt IS NULL));
GO
