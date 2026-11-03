-- Extends the Lab 4 database (Laborator4_PSSC/SQL/create-db.sql) for Lab 5.
-- Safe to run several times.

USE ShoppingCart;
GO

IF COL_LENGTH(N'dbo.Products', N'Stock') IS NULL
    ALTER TABLE dbo.Products
        ADD Stock INT NOT NULL
            CONSTRAINT DF_Products_Stock DEFAULT 0
            CONSTRAINT CK_Products_Stock CHECK (Stock >= 0);
GO

-- Demo catalog, same products as the in-memory catalog from Lab 3.
MERGE dbo.Products AS target
USING (VALUES
    (N'MER-0001', N'Mere (kg)',  4.20, N'Unit', 50),
    (N'LAP-0042', N'Lapte 1L',   6.50, N'Unit', 10),
    (N'PAI-0007', N'Paine',      3.80, N'Unit', 5),
    (N'CAF-0100', N'Cafea 500g', 32.90, N'Unit', 0)
) AS source (Code, Name, Price, QuantityType, Stock)
ON target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Name, Price, QuantityType, Stock)
    VALUES (source.Code, source.Name, source.Price, source.QuantityType, source.Stock);
GO
