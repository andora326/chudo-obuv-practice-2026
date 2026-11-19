namespace ShoeStore.Core;

public enum Role { Customer = 1, Manager = 2, Administrator = 3 }

public sealed class User
{
    public int Id { get; set; }
    public string Login { get; set; }
    public string FullName { get; set; }
    public Role Role { get; set; }
    public User(int id, string login, string fullName, Role role) { Id = id; Login = login; FullName = fullName; Role = role; }
}

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public string Subcategory { get; set; }
    public string Manufacturer { get; set; }
    public string Image { get; set; }
    public string Composition { get; set; }
    public string Description { get; set; }
    public decimal BasePrice { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public Product(int id, string name, string category, string subcategory, string manufacturer, string image, string composition, string description, decimal basePrice, decimal price, int quantity)
    { Id = id; Name = name; Category = category; Subcategory = subcategory; Manufacturer = manufacturer; Image = image; Composition = composition; Description = description; BasePrice = basePrice; Price = price; Quantity = quantity; }
    public string Availability => Quantity > 5 ? "много" : "мало";
    public bool HasDiscount => Price < BasePrice;
    public string OldPriceLabel => HasDiscount ? $"{BasePrice:N2} ₽" : "";
    public string PriceLabel => $"{Price:N2} ₽";
    public string QuantityLabel => $"Наличие: {Availability} ({Quantity} пар)";
}

public sealed class StockItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Size { get; set; }
    public int Quantity { get; set; }
    public StockItem(int id, int productId, decimal size, int quantity) { Id = id; ProductId = productId; Size = size; Quantity = quantity; }
    public string Label => $"{Size:0.##}: в наличии {Quantity} пар";
}

public sealed class CartItem
{
    public int StockItemId { get; set; }
    public int Quantity { get; set; }
    public CartItem(int stockItemId, int quantity) { StockItemId = stockItemId; Quantity = quantity; }
}

public sealed class OrderSummary
{
    public int Id { get; set; }
    public DateTime OrderedAt { get; set; }
    public string Customer { get; set; }
    public decimal Total { get; set; }
    public OrderSummary(int id, DateTime orderedAt, string customer, decimal total) { Id = id; OrderedAt = orderedAt; Customer = customer; Total = total; }
}

public sealed class OrderLine
{
    public int Id { get; set; }
    public int StockItemId { get; set; }
    public string Product { get; set; }
    public string Manufacturer { get; set; }
    public decimal Size { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public OrderLine(int id, int stockItemId, string product, string manufacturer, decimal size, int quantity, decimal unitPrice)
    { Id = id; StockItemId = stockItemId; Product = product; Manufacturer = manufacturer; Size = size; Quantity = quantity; UnitPrice = unitPrice; }
    public decimal Total => Quantity * UnitPrice;
}
