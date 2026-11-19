using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using ShoeStore.Core.Models;

namespace ShoeStore.Core;
public sealed class StoreService
{
    private readonly string connectionString;
    private readonly Func<DateTime> today;
    public StoreService(string sqlConnectionString, Func<DateTime>? clock = null)
    {
        connectionString = sqlConnectionString;
        today = clock ?? (() => DateTime.Today);
    }

    private ShoeStoreEntities Open() => new ShoeStoreEntities(connectionString);
    public void CheckConnection()
    {
        try
        {
            using var db = Open();
            db.Users.Take(1).ToList();
            db.StockItems.Take(1).ToList();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Не удалось подключиться к подготовленной базе. Проверьте базу ShoeTest и параметры подключения в connection-string.txt.", exception);
        }
    }

    private static User UserModel(Users row) => new User(row.Id, row.Login, row.LastName + " " + row.FirstName + " " + row.MiddleName, (Role)row.RoleId);
    public User? Login(string login)
    {
        using var db = Open();
        var value = login.Trim();
        var row = db.Users.AsNoTracking().FirstOrDefault(u => u.Login == value);
        return row is null ? null : UserModel(row);
    }

    private static User RequireUser(ShoeStoreEntities db, User? actor, Role minimum = Role.Customer)
    {
        if (actor is null)
            throw new InvalidOperationException("Войдите по логину, чтобы выполнить это действие.");
        var row = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == actor.Id);
        if (row is null || row.RoleId < (int)minimum)
            throw new InvalidOperationException("У вашей учётной записи нет прав на это действие.");
        return UserModel(row);
    }

    public List<User> Customers(User actor)
    {
        using var db = Open();
        RequireUser(db, actor, Role.Manager);
        return db.Users.AsNoTracking().OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList().Select(UserModel).ToList();
    }

    public List<string> Categories()
    {
        using var db = Open();
        return db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToList();
    }

    public List<Product> Products(string search = "", string? category = null, int sort = 0)
    {
        using var db = Open();
        return ReadProducts(db, search, category, sort);
    }

    private List<Product> ReadProducts(ShoeStoreEntities db, string search = "", string? category = null, int sort = 0)
    {
        var current = today();
        var firstDay = new DateTime(current.Year, current.Month, 1);
        var previousMonth = firstDay.AddMonths(-1);
        var query =
            from p in db.Products.AsNoTracking()join sub in db.Subcategories on p.SubcategoryId equals sub.Id
            join cat in db.Categories on p.CategoryId equals cat.Id
            join manufacturer in db.Manufacturers on p.ManufacturerId equals manufacturer.Id
            select new
            {
                Product = p,
                Category = cat.Name,
                Subcategory = sub.Name,
                Manufacturer = manufacturer.Name,
                Quantity = db.StockItems.Where(s => s.ProductId == p.Id).Sum(s => (int? )s.Quantity) ?? 0,
                WasOrdered = (
                    from l in db.OrderItems
                    join s in db.StockItems on l.StockItemId equals s.Id
                    join o in db.Orders on l.OrderId equals o.Id
                    where s.ProductId == p.Id && o.OrderedAt >= previousMonth && o.OrderedAt < firstDay
                    select l.Id).Any()
            };
        if (category is not null)
            query = query.Where(p => p.Category == category);
        // После материализации доступны decimal.Round и сравнение строк с учётом кириллицы.
        var products = query.ToList().Select(row =>
        {
            var basePrice = row.Product.Price;
            var price = row.WasOrdered ? basePrice : decimal.Round(basePrice * .75m, 2, MidpointRounding.AwayFromZero);
            return new Product(row.Product.Id, row.Product.Name, row.Category, row.Subcategory, row.Manufacturer, Path.GetFileName((row.Product.Image ?? "picture.png").Replace('\\', '/')), row.Product.Composition, row.Product.Description, basePrice, price, row.Quantity);
        });
        var term = search.Trim();
        var filtered = products.Where(p => p.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || p.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        return (sort switch
        {
            1 => filtered.OrderBy(p => p.Price),
            2 => filtered.OrderByDescending(p => p.Price),
            _ => filtered.OrderBy(p => p.Id)}).ThenBy(p => p.Id).ToList();
    }

    public List<StockItem> Stock(int productId, bool availableOnly = true)
    {
        using var db = Open();
        var query =
            from s in db.StockItems.AsNoTracking()join size in db.Sizes on s.SizeId equals size.Id
            where s.ProductId == productId && (!availableOnly || s.Quantity > 0)orderby size.Value
            select new
            {
                s.Id,
                s.ProductId,
                Size = size.Value,
                s.Quantity
            };
        return query.ToList().Select(s => new StockItem(s.Id, s.ProductId, (decimal)s.Size, s.Quantity)).ToList();
    }

    private static void Save(ShoeStoreEntities db)
    {
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Остаток уже изменился. Обновите каталог и повторите заказ с доступным количеством.");
        }
    }

    public int PlaceOrder(User? actor, IEnumerable<CartItem> cart, int? customerId = null)
    {
        var lines = cart.ToList();
        if (lines.Count == 0 || lines.Any(l => l.Quantity <= 0))
            throw new InvalidOperationException("Добавьте товары в заказ и укажите положительное целое количество.");
        var grouped = lines.GroupBy(l => l.StockItemId).Select(g => new CartItem(g.Key, checked(g.Sum(l => l.Quantity)))).OrderBy(l => l.StockItemId).ToList();
        using var db = Open();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.ReadCommitted);
        var user = RequireUser(db, actor);
        var target = customerId ?? user.Id;
        if (target != user.Id)
            RequireUser(db, actor, Role.Manager);
        var order = new Models.Orders
        {
            OrderedAt = today().Date,
            UserId = target
        };
        db.Orders.Add(order);
        Save(db);
        foreach (var line in grouped)
        {
            var stock = db.StockItems.SingleOrDefault(s => s.Id == line.StockItemId);
            if (stock is null)
                throw new InvalidOperationException("Товарная позиция не найдена. Обновите каталог.");
            if (stock.Quantity < line.Quantity)
                throw new InvalidOperationException("Недостаточно товара выбранного размера. Уменьшите количество или обновите каталог.");
            stock.Quantity -= line.Quantity;
            db.OrderItems.Add(new Models.OrderItems { OrderId = order.Id, StockItemId = stock.Id, Quantity = line.Quantity });
        }

        // ConcurrencyMode=Fixed в EDMX заставляет EF проверять исходный остаток при UPDATE.
        Save(db);
        transaction.Commit();
        return order.Id;
    }

    public List<OrderSummary> Orders(User actor)
    {
        using var db = Open();
        RequireUser(db, actor, Role.Manager);
        var prices = ReadProducts(db).ToDictionary(p => p.Id, p => p.Price);
        var items = (
            from line in db.OrderItems.AsNoTracking()join stock in db.StockItems on line.StockItemId equals stock.Id
            select new
            {
                line.OrderId,
                stock.ProductId,
                line.Quantity
            }

        ).ToList();
        var totals = items.GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => g.Sum(x => prices[x.ProductId] * x.Quantity));
        var orders = (
            from order in db.Orders.AsNoTracking()join user in db.Users on order.UserId equals user.Id
            orderby order.OrderedAt descending, order.Id descending
            select new
            {
                order.Id,
                order.OrderedAt,
                Customer = user.LastName + " " + user.FirstName + " " + user.MiddleName
            }

        ).ToList();
        return orders.Select(o => new OrderSummary(o.Id, o.OrderedAt, o.Customer, totals.TryGetValue(o.Id, out var total) ? total : 0)).ToList();
    }

    public List<OrderLine> OrderLines(User actor, int orderId)
    {
        using var db = Open();
        RequireUser(db, actor, Role.Manager);
        var prices = ReadProducts(db).ToDictionary(p => p.Id, p => p.Price);
        var query =
            from l in db.OrderItems.AsNoTracking()join s in db.StockItems on l.StockItemId equals s.Id
            join p in db.Products on s.ProductId equals p.Id
            join m in db.Manufacturers on p.ManufacturerId equals m.Id
            join size in db.Sizes on s.SizeId equals size.Id
            where l.OrderId == orderId
            orderby l.Id
            select new
            {
                l.Id,
                StockId = s.Id,
                ProductId = p.Id,
                Product = p.Name,
                Manufacturer = m.Name,
                Size = size.Value,
                l.Quantity
            };
        return query.ToList().Select(l => new OrderLine(l.Id, l.StockId, l.Product, l.Manufacturer, (decimal)l.Size, l.Quantity, prices[l.ProductId])).ToList();
    }

    public void DeleteOrder(User actor, int orderId)
    {
        using var db = Open();
        using var transaction = db.Database.BeginTransaction();
        RequireUser(db, actor, Role.Manager);
        var order = db.Orders.SingleOrDefault(o => o.Id == orderId) ?? throw new InvalidOperationException("Заказ не найден. Обновите список.");
        var lines = db.OrderItems.Where(l => l.OrderId == orderId).OrderBy(l => l.StockItemId).ToList();
        foreach (var line in lines)
            db.StockItems.Single(s => s.Id == line.StockItemId).Quantity += line.Quantity;
        db.OrderItems.RemoveRange(lines);
        db.Orders.Remove(order);
        Save(db);
        transaction.Commit();
    }

    public void ChangeOrderDate(User actor, int orderId, DateTime date)
    {
        if (date.Date > today().Date)
            throw new InvalidOperationException("Дата заказа не может быть позже сегодняшней даты.");
        using var db = Open();
        RequireUser(db, actor, Role.Administrator);
        var order = db.Orders.SingleOrDefault(o => o.Id == orderId) ?? throw new InvalidOperationException("Заказ не найден. Обновите список.");
        order.OrderedAt = date.Date;
        Save(db);
    }

    public void RemoveOrderLine(User actor, int orderId, int lineId)
    {
        using var db = Open();
        using var transaction = db.Database.BeginTransaction();
        RequireUser(db, actor, Role.Administrator);
        if (db.OrderItems.Count(l => l.OrderId == orderId) <= 1)
            throw new InvalidOperationException("Это последняя позиция. Для отмены заказа используйте «Удалить заказ».");
        var line = db.OrderItems.SingleOrDefault(l => l.Id == lineId && l.OrderId == orderId) ?? throw new InvalidOperationException("Позиция заказа не найдена.");
        db.StockItems.Single(s => s.Id == line.StockItemId).Quantity += line.Quantity;
        db.OrderItems.Remove(line);
        Save(db);
        transaction.Commit();
    }
}
