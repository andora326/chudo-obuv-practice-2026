using System.Data.SqlClient;
using ShoeStore.Core;

var passed = 0;
var output = new List<string>();
var root = Path.Combine(Path.GetTempPath(), "ChudoObuvChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var databaseName = "ChudoObuvTest_" + Guid.NewGuid().ToString("N");
var database = new SqlConnectionStringBuilder(DatabaseSettings.DefaultConnectionString) { InitialCatalog = databaseName }.ToString();
var now = new DateTime(2026, 5, 15);
var store = new StoreService(database, () => now);
var masterString = new SqlConnectionStringBuilder(database) { InitialCatalog = "master" }.ToString();
using (var master = new SqlConnection(masterString))
{
    master.Open();
    using var create = new SqlCommand("CREATE DATABASE [" + databaseName + "]", master);
    create.ExecuteNonQuery();
}
using (var setup = new SqlConnection(database))
{
    setup.Open();
    foreach (var file in new[] { "schema.sql", "seed.sql" })
    {
        using var command = new SqlCommand(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", file)), setup);
        command.ExecuteNonQuery();
    }
}
store.CheckConnection();
string LoginForRole(int role)
{
    using var connection = new SqlConnection(database);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT TOP(1) Login FROM Users WHERE RoleId=@role ORDER BY Id";
    command.Parameters.AddWithValue("@role",role);
    return (string)command.ExecuteScalar()!;
}
var admin = store.Login(LoginForRole(3))!;
var manager = store.Login(LoginForRole(2))!;
var customer = store.Login(LoginForRole(1))!;

void Check(string name, Action action)
{
    action();
    passed++;
    output.Add($"PASS {passed:00} {name}");
    Console.WriteLine(output[output.Count - 1]);
}
void Assert(bool condition, string message = "Проверка не выполнена") { if (!condition) throw new Exception(message); }
void Reject(Action action)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Ожидался отказ операции");
}
long Scalar(string sql)
{
    using var connection = new SqlConnection(database);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt64(command.ExecuteScalar());
}
void Execute(string sql)
{
    using var connection = new SqlConnection(database);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.ExecuteNonQuery();
}

Check("Импорт: 31 модель, 92 позиции, 20 пользователей, 10 заказов, 30 строк", () => Assert(store.Products().Count == 31 && Scalar("SELECT COUNT(*) FROM StockItems") == 92 && Scalar("SELECT COUNT(*) FROM Users") == 20 && store.Orders(admin).Count == 10 && Scalar("SELECT COUNT(*) FROM OrderItems") == 30));
Check("Подключение к готовой базе и отказ без автосоздания", () => {
    store.CheckConnection();
    Assert(store.Products().Count == 31);
    var missingName = "ChudoObuvMissing_" + Guid.NewGuid().ToString("N");
    var missingConnection = new SqlConnectionStringBuilder(database) { InitialCatalog = missingName }.ToString();
    Reject(() => new StoreService(missingConnection).CheckConnection());
    using var master = new SqlConnection(masterString);
    master.Open();
    using var lookup = new SqlCommand("SELECT DB_ID(@name)", master);
    lookup.Parameters.AddWithValue("@name", missingName);
    Assert(lookup.ExecuteScalar() == DBNull.Value, "Программа не должна создавать отсутствующую базу");
});
Check("Вход по логину: пробелы и регистр; неизвестный логин", () => Assert(store.Login(" " + admin.Login.ToUpperInvariant() + " ") == admin && store.Login("unknown") is null));
Check("Дробные размеры не округляются", () => Assert(store.Products().SelectMany(p => store.Stock(p.Id, false)).Any(s => s.Size == 38.5m)));
Check("Заказы в апреле отменяют скидку в мае", () => Assert(store.Products().First(p => p.Id == 1).Price == store.Products().First(p => p.Id == 1).BasePrice));
Check("Нет заказов в прошлом месяце — скидка 25 процентов", () => { now = new DateTime(2026,10,29); Assert(store.Products().All(p => p.Price == decimal.Round(p.BasePrice * .75m, 2, MidpointRounding.AwayFromZero))); });
Check("Поиск на кириллице без учёта регистра по описанию", () => Assert(store.Products("СМОЛЕНСК").Count > 0));
Check("Поиск, категория и сортировка применяются совместно", () => { var list = store.Products("кроссовки", "Женская обувь", 2); Assert(list.Count > 0 && list.All(p => p.Category == "Женская обувь") && list.Select(p => p.Price).SequenceEqual(list.Select(p => p.Price).OrderByDescending(p => p))); });
Check("Неизвестный запрос возвращает пустой каталог", () => Assert(store.Products("неизвестныйтовар123").Count == 0));
Check("Отсутствующее изображение заменяется заглушкой", () => { Execute("UPDATE Products SET imagePath=NULL WHERE Id=31"); Assert(store.Products().First(p => p.Id == 31).Image == "picture.png"); });
var stock = store.Stock(1).First();
var stock2 = store.Stock(2).First();
var orderId = 0;
Check("Оформление заказа уменьшает остатки и рассчитывает сумму", () => { orderId = store.PlaceOrder(customer, [new(stock.Id, 2), new(stock2.Id, 1)]); Assert(store.Stock(1).First(s => s.Id == stock.Id).Quantity == stock.Quantity - 2); var lines = store.OrderLines(admin, orderId); Assert(lines.Count == 2 && lines.First().UnitPrice == store.Products().First(p => p.Id == 1).Price); });
Check("Изменение даты запрещено менеджеру и клиенту", () => { Reject(() => store.ChangeOrderDate(manager, orderId, now)); Reject(() => store.ChangeOrderDate(customer, orderId, now)); });
Check("Администратор изменяет дату заказа", () => { store.ChangeOrderDate(admin, orderId, now.AddDays(-1)); Assert(store.Orders(admin).First(o => o.Id == orderId).OrderedAt == now.AddDays(-1)); });
Check("Будущая дата запрещена", () => Reject(() => store.ChangeOrderDate(admin, orderId, now.AddDays(1))));
Check("Менеджер не может удалить строку заказа", () => Reject(() => store.RemoveOrderLine(manager, orderId, store.OrderLines(admin, orderId)[0].Id)));
Check("Удаление строки администратором возвращает остаток", () => { var line = store.OrderLines(admin, orderId).First(); store.RemoveOrderLine(admin, orderId, line.Id); Assert(store.Stock(1).First(s => s.Id == stock.Id).Quantity == stock.Quantity && store.OrderLines(admin, orderId).Count == 1); });
Check("Последнюю строку удалить нельзя", () => Reject(() => store.RemoveOrderLine(admin, orderId, store.OrderLines(admin, orderId)[0].Id)));
Check("Удаление заказа менеджером возвращает остатки", () => { store.DeleteOrder(manager, orderId); Assert(store.Stock(2).First(s => s.Id == stock2.Id).Quantity == stock2.Quantity && store.Orders(admin).Count == 10); });
Check("Нехватка второго товара откатывает весь заказ", () => { var count = store.Orders(admin).Count; Reject(() => store.PlaceOrder(customer, [new(stock.Id, 1), new(stock2.Id, int.MaxValue)])); Assert(store.Stock(1).First(s => s.Id == stock.Id).Quantity == stock.Quantity && store.Orders(admin).Count == count); });
Check("Повторяющиеся позиции объединяются до проверки остатка", () => Reject(() => store.PlaceOrder(customer, [new(stock.Id, stock.Quantity), new(stock.Id, 1)])));
Check("Нулевое количество, пустой заказ и неизвестный размер запрещены", () => { Reject(() => store.PlaceOrder(customer, [new(stock.Id, 0)])); Reject(() => store.PlaceOrder(customer, [])); Reject(() => store.PlaceOrder(customer, [new(999999, 1)])); });
Check("Гость не оформляет заказ; клиент не читает и не удаляет чужие заказы", () => { Reject(() => store.PlaceOrder(null, [new(stock.Id, 1)])); Reject(() => store.Orders(customer)); Reject(() => store.OrderLines(customer, 1)); Reject(() => store.DeleteOrder(customer, 1)); Reject(() => store.Customers(customer)); });
Check("Подмена роли в объекте пользователя не даёт прав", () => Reject(() => store.DeleteOrder(customer with { Role = Role.Administrator }, 1)));
Check("Клиент не может создать заказ от имени другого пользователя", () => Reject(() => store.PlaceOrder(customer, [new(stock.Id,1)], admin.Id)));
Check("Менеджер создаёт заказ выбранному клиенту", () => { var id = store.PlaceOrder(manager, [new(stock.Id,1)], customer.Id); Assert(store.Orders(manager).First(o => o.Id == id).Customer == customer.FullName); store.DeleteOrder(manager,id); });
Check("Декабрь учитывается как предыдущий месяц января", () => { now = new DateTime(2027,1,4); var id = store.PlaceOrder(customer, [new(stock.Id,1)]); store.ChangeOrderDate(admin,id,new DateTime(2026,12,31)); Assert(!store.Products().First(p => p.Id == 1).HasDiscount); store.DeleteOrder(admin,id); });
Check("Граница месяца: первое число текущего месяца не учитывается", () => { now = new DateTime(2026,10,29); var id = store.PlaceOrder(customer, [new(stock.Id,1)]); store.ChangeOrderDate(admin,id,new DateTime(2026,10,1)); Assert(store.Products().First(p => p.Id == 1).HasDiscount); store.ChangeOrderDate(admin,id,new DateTime(2026,9,1)); Assert(!store.Products().First(p => p.Id == 1).HasDiscount); store.DeleteOrder(admin,id); });
Check("Одновременная покупка последней пары: ровно один успех", () => { Execute($"UPDATE StockItems SET Quantity=1 WHERE Id={stock.Id}"); var successes = 0; var attempts = Enumerable.Range(0,2).Select(_ => Task.Run(() => { try { store.PlaceOrder(customer,[new(stock.Id,1)]); Interlocked.Increment(ref successes); } catch (InvalidOperationException) { } })).ToArray(); Task.WaitAll(attempts); Assert(successes == 1 && Scalar($"SELECT Quantity FROM StockItems WHERE Id={stock.Id}") == 0); });
Check("Ссылочная целостность базы после операций", () => { using var c = new SqlConnection(database); c.Open(); using var command = c.CreateCommand(); command.CommandText = "DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS"; using var reader = command.ExecuteReader(); Assert(!reader.Read()); });
var summary = $"SUCCESS: {passed} checks passed. Fixed clock, isolated temporary database.";
Console.WriteLine(summary);
if (args.Length > 0) File.WriteAllLines(args[0], output.Append(summary));
SqlConnection.ClearAllPools();
var masterBuilder = new SqlConnectionStringBuilder(database) { InitialCatalog = "master" };
using (var master = new SqlConnection(masterBuilder.ConnectionString))
{
    master.Open();
    using var drop = master.CreateCommand();
    if (!databaseName.StartsWith("ChudoObuvTest_") || databaseName.Length != 46)
        throw new InvalidOperationException("Некорректное имя временной базы");
    drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
    drop.ExecuteNonQuery();
}
Directory.Delete(root, true);

