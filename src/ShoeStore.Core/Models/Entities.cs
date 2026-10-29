namespace ShoeStore.Core.Models;

public partial class Roles
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public partial class Users
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public int RoleId { get; set; }
}

public partial class Categories
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public partial class Subcategories
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
}

public partial class Manufacturers
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public partial class Products
{
    public int Id { get; set; }
    public int SubcategoryId { get; set; }
    public int ManufacturerId { get; set; }
    public string Name { get; set; } = "";
    public string Image { get; set; } = "";
    public string Description { get; set; } = "";
    public string Composition { get; set; } = "";
    public long PriceCents { get; set; }
}

public partial class Sizes
{
    public int Id { get; set; }
    public decimal Value { get; set; }
}

public partial class StockItems
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int SizeId { get; set; }
    public int Quantity { get; set; }
}

public partial class Orders
{
    public int Id { get; set; }
    public DateTime OrderedAt { get; set; }
    public int UserId { get; set; }
}

public partial class OrderLines
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int StockItemId { get; set; }
    public int Quantity { get; set; }
    public long UnitPriceCents { get; set; }
}
