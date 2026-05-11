IF OBJECT_ID(N'dbo.ufn_SE_CustomerDisplayName', N'FN') IS NOT NULL
  DROP FUNCTION dbo.ufn_SE_CustomerDisplayName;
GO

IF OBJECT_ID(N'dbo.usp_SE_GetCustomers', N'P') IS NOT NULL
  DROP PROCEDURE dbo.usp_SE_GetCustomers;
GO

IF OBJECT_ID(N'dbo.SE_ActiveCustomers', N'V') IS NOT NULL
  DROP VIEW dbo.SE_ActiveCustomers;
GO

IF OBJECT_ID(N'dbo.SE_Orders', N'U') IS NOT NULL
  DROP TABLE dbo.SE_Orders;
GO

IF OBJECT_ID(N'dbo.SE_Customers', N'U') IS NOT NULL
  DROP TABLE dbo.SE_Customers;
GO

CREATE TABLE dbo.SE_Customers (
  Id int IDENTITY(1, 1) NOT NULL,
  Name nvarchar(100) NOT NULL,
  Email nvarchar(256) NULL,
  IsActive bit NOT NULL CONSTRAINT DF_SE_Customers_IsActive DEFAULT (1),
  CONSTRAINT PK_SE_Customers PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE TABLE dbo.SE_Orders (
  Id int IDENTITY(1, 1) NOT NULL,
  CustomerId int NOT NULL,
  OrderNumber nvarchar(40) NOT NULL,
  TotalAmount decimal(18, 2) NOT NULL,
  CONSTRAINT PK_SE_Orders PRIMARY KEY CLUSTERED (Id),
  CONSTRAINT FK_SE_Orders_SE_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.SE_Customers (Id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_SE_Customers_Email
ON dbo.SE_Customers (Email)
INCLUDE (Name);
GO

EXEC sys.sp_addextendedproperty
  @name = N'MS_Description',
  @value = N'SchemaExporter fixture customer table',
  @level0type = N'SCHEMA',
  @level0name = N'dbo',
  @level1type = N'TABLE',
  @level1name = N'SE_Customers';
GO

EXEC sys.sp_addextendedproperty
  @name = N'MS_Description',
  @value = N'Customer display name',
  @level0type = N'SCHEMA',
  @level0name = N'dbo',
  @level1type = N'TABLE',
  @level1name = N'SE_Customers',
  @level2type = N'COLUMN',
  @level2name = N'Name';
GO

CREATE VIEW dbo.SE_ActiveCustomers
AS
SELECT
  Id,
  Name,
  Email
FROM dbo.SE_Customers
WHERE IsActive = 1;
GO

CREATE PROCEDURE dbo.usp_SE_GetCustomers
  @OnlyActive bit = 1
AS
BEGIN
  SET NOCOUNT ON;

  SELECT
    Id,
    Name,
    Email
  FROM dbo.SE_Customers
  WHERE @OnlyActive = 0 OR IsActive = 1;
END;
GO

CREATE FUNCTION dbo.ufn_SE_CustomerDisplayName (
  @Name nvarchar(100),
  @Email nvarchar(256)
)
RETURNS nvarchar(400)
AS
BEGIN
  RETURN CONCAT(@Name, N' <', @Email, N'>');
END;
GO
