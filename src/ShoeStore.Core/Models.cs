namespace ShoeStore.Core;
public enum Role
{
    Customer = 1,
    Manager = 2,
    Administrator = 3
}

public sealed record User(int Id, string Login, string FullName, Role Role);
public sealed record Product(int Id, string Name, string Category, string Subcategory, string Manufacturer, string Image, string Composition, string Description, decimal BasePrice, decimal Price, int Quantity)
{
    public string Availability => Quantity > 5 ? "много" : "мало";
    public bool HasDiscount => Price < BasePrice;
    public string OldPriceLabel => HasDiscount ? $"{BasePrice:N2} ₽" : "";
    public string PriceLabel => $"{Price:N2} ₽" + (HasDiscount ? "  • скидка 25%" : "");
    public string QuantityLabel => $"Наличие: {Availability} ({Quantity} пар)";
}

public sealed record StockItem(int Id, int ProductId, decimal Size, int Quantity)
{
    public string Label => $"{Size:0.##}: в наличии {Quantity} пар";
}

public sealed record CartItem(int StockItemId, int Quantity);
public sealed record OrderSummary(int Id, DateTime OrderedAt, string Customer, decimal Total);
public sealed record OrderLine(int Id, int StockItemId, string Product, string Manufacturer, decimal Size, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}
