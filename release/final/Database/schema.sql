CREATE TABLE dbo.[Roles](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [name] nvarchar(50) NOT NULL
);

CREATE TABLE dbo.[Users](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [surname] nvarchar(50) NOT NULL,
    [firstname] nvarchar(50) NOT NULL,
    [patronomyc] nvarchar(50) NOT NULL,
    [login] nvarchar(50) NOT NULL,
    [roleId] int NOT NULL
);

CREATE TABLE dbo.[Categories](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [name] nvarchar(50) NOT NULL
);

CREATE TABLE dbo.[Subcategories](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [name] nvarchar(50) NOT NULL
);

CREATE TABLE dbo.[Manufacturers](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [name] nvarchar(50) NOT NULL
);

CREATE TABLE dbo.[Products](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [categoryId] int NOT NULL,
    [subcategoryId] int NOT NULL,
    [imagePath] nvarchar(150) NULL,
    [name] nvarchar(250) NOT NULL,
    [manufacturerId] int NOT NULL,
    [description] nvarchar(250) NOT NULL,
    [composition] nvarchar(250) NOT NULL,
    [price] decimal(18,0) NOT NULL
);

CREATE TABLE dbo.[Sizes](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [size] float NOT NULL
);

CREATE TABLE dbo.[StockItems](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [productId] int NOT NULL,
    [sizeId] int NOT NULL,
    [quantity] int NOT NULL
);

CREATE TABLE dbo.[Orders](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [date] date NOT NULL,
    [userId] int NOT NULL
);

CREATE TABLE dbo.[OrderItems](
    [id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [orderId] int NOT NULL,
    [stockItemId] int NOT NULL,
    [quantity] int NOT NULL
);

ALTER TABLE dbo.[Users] ADD CONSTRAINT [FK_Users_roleId] FOREIGN KEY ([roleId]) REFERENCES dbo.[Roles]([id]);

ALTER TABLE dbo.[Products] ADD CONSTRAINT [FK_Products_categoryId] FOREIGN KEY ([categoryId]) REFERENCES dbo.[Categories]([id]);

ALTER TABLE dbo.[Products] ADD CONSTRAINT [FK_Products_subcategoryId] FOREIGN KEY ([subcategoryId]) REFERENCES dbo.[Subcategories]([id]);

ALTER TABLE dbo.[Products] ADD CONSTRAINT [FK_Products_manufacturerId] FOREIGN KEY ([manufacturerId]) REFERENCES dbo.[Manufacturers]([id]);

ALTER TABLE dbo.[StockItems] ADD CONSTRAINT [FK_StockItems_productId] FOREIGN KEY ([productId]) REFERENCES dbo.[Products]([id]);

ALTER TABLE dbo.[StockItems] ADD CONSTRAINT [FK_StockItems_sizeId] FOREIGN KEY ([sizeId]) REFERENCES dbo.[Sizes]([id]);

ALTER TABLE dbo.[Orders] ADD CONSTRAINT [FK_Orders_userId] FOREIGN KEY ([userId]) REFERENCES dbo.[Users]([id]);

ALTER TABLE dbo.[OrderItems] ADD CONSTRAINT [FK_OrderItems_orderId] FOREIGN KEY ([orderId]) REFERENCES dbo.[Orders]([id]);

ALTER TABLE dbo.[OrderItems] ADD CONSTRAINT [FK_OrderItems_stockItemId] FOREIGN KEY ([stockItemId]) REFERENCES dbo.[StockItems]([id]);
