using System.Data;
using Microsoft.Data.SqlClient;

namespace ShoeStore.Core;

/// <summary>Доступ к данным и правила заказа. Проверки ролей выполняются здесь, независимо от интерфейса.</summary>
public sealed class StoreService
{
    private readonly string connectionString;
    private readonly Func<DateTime> today;

    public StoreService(string sqlConnectionString, Func<DateTime>? clock = null)
    {
        connectionString = sqlConnectionString;
        today = clock ?? (() => DateTime.Today);
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static SqlCommand Command(SqlConnection connection, string sql, params object[] values) => Command(connection, null, sql, values);

    private static SqlCommand Command(SqlConnection connection, SqlTransaction? transaction, string sql, params object[] values)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        for (var i = 0; i < values.Length; i++)
            command.Parameters.AddWithValue("@p" + i, values[i]);
        return command;
    }

    public void Initialize(string schemaPath, string seedPath)
    {
        DatabaseSettings.EnsureDatabase(connectionString);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var check = Command(connection, transaction, "SELECT COUNT(*) FROM sys.tables WHERE name=N'Users' AND schema_id=SCHEMA_ID(N'dbo')");
        if (Convert.ToInt32(check.ExecuteScalar()) != 0)
            return;
        using var schema = Command(connection, transaction, File.ReadAllText(schemaPath));
        schema.ExecuteNonQuery();
        using var seed = Command(connection, transaction, File.ReadAllText(seedPath));
        seed.ExecuteNonQuery();
        transaction.Commit();
    }

    public User? Login(string login)
    {
        using var connection = Open();
        using var command = Command(connection, "SELECT Id,Login,LastName + N' ' + FirstName + N' ' + MiddleName,RoleId FROM Users WHERE Login=@p0", login.Trim());
        using var reader = command.ExecuteReader();
        return reader.Read() ? new User(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), (Role)reader.GetInt32(3)) : null;
    }

    private User RequireUser(SqlConnection connection, User? actor, Role minimum = Role.Customer, SqlTransaction? transaction = null)
    {
        if (actor is null)
            throw new InvalidOperationException("Войдите по логину, чтобы выполнить это действие.");
        using var command = Command(connection, transaction, "SELECT Id,Login,LastName + N' ' + FirstName + N' ' + MiddleName,RoleId FROM Users WHERE Id=@p0", actor.Id);
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(3) < (int)minimum)
            throw new InvalidOperationException("У вашей учётной записи нет прав на это действие.");
        return new User(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), (Role)reader.GetInt32(3));
    }

    public List<User> Customers(User actor)
    {
        using var connection = Open();
        RequireUser(connection, actor, Role.Manager);
        using var command = Command(connection, "SELECT Id,Login,LastName + N' ' + FirstName + N' ' + MiddleName,RoleId FROM Users ORDER BY LastName,FirstName");
        using var reader = command.ExecuteReader();
        var users = new List<User>();
        while (reader.Read())
            users.Add(new User(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), (Role)reader.GetInt32(3)));
        return users;
    }

    public List<string> Categories()
    {
        using var connection = Open();
        using var command = Command(connection, "SELECT Name FROM Categories ORDER BY Name");
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
            values.Add(reader.GetString(0));
        return values;
    }

    public List<Product> Products(string search = "", string? category = null, int sort = 0)
    {
        using var connection = Open();
        return ReadProducts(connection, null, search, category, sort);
    }

    private List<Product> ReadProducts(SqlConnection connection, SqlTransaction? transaction, string search = "", string? category = null, int sort = 0)
    {
        var firstDay = new DateTime(today().Year, today().Month, 1);
        using var command = Command(connection, transaction, """
            SELECT p.Id,p.Name,c.Name,sc.Name,m.Name,p.Image,p.Composition,p.Description,p.PriceCents,
              COALESCE((SELECT SUM(s.Quantity) FROM StockItems s WHERE s.ProductId=p.Id),0),
              CAST(CASE WHEN EXISTS(SELECT 1 FROM OrderLines l JOIN StockItems s ON s.Id=l.StockItemId JOIN Orders o ON o.Id=l.OrderId
                     WHERE s.ProductId=p.Id AND o.OrderedAt>=@p0 AND o.OrderedAt<@p1) THEN 1 ELSE 0 END AS bit)
            FROM Products p JOIN Subcategories sc ON sc.Id=p.SubcategoryId JOIN Categories c ON c.Id=sc.CategoryId
            JOIN Manufacturers m ON m.Id=p.ManufacturerId
            """, firstDay.AddMonths(-1), firstDay);
        using var reader = command.ExecuteReader();
        var products = new List<Product>();
        while (reader.Read())
        {
            var basePrice = reader.GetInt64(8) / 100m;
            var price = reader.GetBoolean(10) ? basePrice : decimal.Round(basePrice * 0.75m, 2, MidpointRounding.AwayFromZero);
            products.Add(new Product(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), basePrice, price, reader.GetInt32(9)));
        }
        // Одинаковый поиск по кириллице независимо от настроек сортировки SQL Server.
        var filtered = products.Where(p => (category is null || p.Category == category) &&
            (p.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) || p.Description.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)));
        return (sort switch { 1 => filtered.OrderBy(p => p.Price), 2 => filtered.OrderByDescending(p => p.Price), _ => filtered.OrderBy(p => p.Id) }).ThenBy(p => p.Id).ToList();
    }

    public List<StockItem> Stock(int productId, bool availableOnly = true)
    {
        using var connection = Open();
        using var command = Command(connection, "SELECT s.Id,s.ProductId,z.Value,s.Quantity FROM StockItems s JOIN Sizes z ON z.Id=s.SizeId WHERE s.ProductId=@p0 ORDER BY z.Value", productId);
        using var reader = command.ExecuteReader();
        var stock = new List<StockItem>();
        while (reader.Read())
            if (!availableOnly || reader.GetInt32(3) > 0)
                stock.Add(new StockItem(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetInt32(3)));
        return stock;
    }

    public int PlaceOrder(User? actor, IEnumerable<CartItem> cart, int? customerId = null)
    {
        var lines = cart.ToList();
        if (lines.Count == 0 || lines.Any(l => l.Quantity <= 0))
            throw new InvalidOperationException("Добавьте товары в заказ и укажите положительное целое количество.");
        var grouped = lines.GroupBy(l => l.StockItemId).Select(g => new CartItem(g.Key, checked(g.Sum(l => l.Quantity)))).OrderBy(l => l.StockItemId).ToList();
        using var connection = Open();
        // Условное списание в транзакции не позволяет продать отсутствующий остаток.
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var user = RequireUser(connection, actor, Role.Customer, transaction);
        var target = customerId ?? user.Id;
        if (target != user.Id)
            RequireUser(connection, actor, Role.Manager, transaction);
        var prices = ReadProducts(connection, transaction).ToDictionary(p => p.Id, p => p.Price);
        using var insert = Command(connection, transaction, "INSERT INTO Orders(OrderedAt,UserId) OUTPUT INSERTED.Id VALUES(@p0,@p1)", today().Date, target);
        var orderId = (int)insert.ExecuteScalar()!;
        foreach (var line in grouped)
        {
            using var get = Command(connection, transaction, "SELECT ProductId FROM StockItems WHERE Id=@p0", line.StockItemId);
            var productId = get.ExecuteScalar();
            if (productId is null)
                throw new InvalidOperationException("Товарная позиция не найдена. Обновите каталог.");
            using var update = Command(connection, transaction, "UPDATE StockItems SET Quantity=Quantity-@p0 WHERE Id=@p1 AND Quantity>=@p0", line.Quantity, line.StockItemId);
            if (update.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Недостаточно товара выбранного размера. Уменьшите количество или обновите каталог.");
            using var add = Command(connection, transaction, "INSERT INTO OrderLines(OrderId,StockItemId,Quantity,UnitPriceCents) VALUES(@p0,@p1,@p2,@p3)", orderId, line.StockItemId, line.Quantity, (long)(prices[Convert.ToInt32(productId)] * 100m));
            add.ExecuteNonQuery();
        }
        transaction.Commit();
        return orderId;
    }

    public List<OrderSummary> Orders(User actor)
    {
        using var connection = Open();
        RequireUser(connection, actor, Role.Manager);
        using var command = Command(connection, """
            SELECT o.Id,o.OrderedAt,u.LastName + N' ' + u.FirstName + N' ' + u.MiddleName,COALESCE((SELECT SUM(l.Quantity*l.UnitPriceCents) FROM OrderLines l WHERE l.OrderId=o.Id),0)
            FROM Orders o JOIN Users u ON u.Id=o.UserId
            ORDER BY o.OrderedAt DESC,o.Id DESC
            """);
        using var reader = command.ExecuteReader();
        var orders = new List<OrderSummary>();
        while (reader.Read())
            orders.Add(new OrderSummary(reader.GetInt32(0), reader.GetDateTime(1), reader.GetString(2), reader.GetInt64(3) / 100m));
        return orders;
    }

    public List<OrderLine> OrderLines(User actor, int orderId)
    {
        using var connection = Open();
        RequireUser(connection, actor, Role.Manager);
        using var command = Command(connection, """
            SELECT l.Id,s.Id,p.Name,m.Name,z.Value,l.Quantity,l.UnitPriceCents FROM OrderLines l
            JOIN StockItems s ON s.Id=l.StockItemId JOIN Products p ON p.Id=s.ProductId
            JOIN Manufacturers m ON m.Id=p.ManufacturerId JOIN Sizes z ON z.Id=s.SizeId WHERE l.OrderId=@p0 ORDER BY l.Id
            """, orderId);
        using var reader = command.ExecuteReader();
        var lines = new List<OrderLine>();
        while (reader.Read())
            lines.Add(new OrderLine(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetInt32(5), reader.GetInt64(6) / 100m));
        return lines;
    }

    public void DeleteOrder(User actor, int orderId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        RequireUser(connection, actor, Role.Manager, transaction);
        using var restore = Command(connection, transaction, "UPDATE s SET Quantity=s.Quantity+COALESCE((SELECT SUM(l.Quantity) FROM OrderLines l WHERE l.StockItemId=s.Id AND l.OrderId=@p0),0) FROM StockItems s", orderId);
        restore.ExecuteNonQuery();
        using var delete = Command(connection, transaction, "DELETE FROM Orders WHERE Id=@p0", orderId);
        if (delete.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Заказ не найден. Обновите список.");
        transaction.Commit();
    }

    public void ChangeOrderDate(User actor, int orderId, DateTime date)
    {
        if (date.Date > today().Date)
            throw new InvalidOperationException("Дата заказа не может быть позже сегодняшней даты.");
        using var connection = Open();
        RequireUser(connection, actor, Role.Administrator);
        using var command = Command(connection, "UPDATE Orders SET OrderedAt=@p0 WHERE Id=@p1", date.Date, orderId);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Заказ не найден. Обновите список.");
    }

    public void RemoveOrderLine(User actor, int orderId, int lineId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        RequireUser(connection, actor, Role.Administrator, transaction);
        using var check = Command(connection, transaction, "SELECT COUNT(*) FROM OrderLines WHERE OrderId=@p0", orderId);
        if (Convert.ToInt32(check.ExecuteScalar()) <= 1)
            throw new InvalidOperationException("Это последняя позиция. Для отмены заказа используйте «Удалить заказ».");
        using var restore = Command(connection, transaction, "UPDATE StockItems SET Quantity=Quantity+(SELECT Quantity FROM OrderLines WHERE Id=@p0 AND OrderId=@p1) WHERE Id=(SELECT StockItemId FROM OrderLines WHERE Id=@p0 AND OrderId=@p1)", lineId, orderId);
        if (restore.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Позиция заказа не найдена.");
        using var delete = Command(connection, transaction, "DELETE FROM OrderLines WHERE Id=@p0 AND OrderId=@p1", lineId, orderId);
        delete.ExecuteNonQuery();
        transaction.Commit();
    }
}
