using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ShoeStore.Core;

namespace ShoeStore.App;

public sealed record ProductRow(Product Product, string ImagePath)
{
    public bool LowStock => Product.Quantity <= 3;
}
public sealed record BasketRow(int StockItemId, string Name, decimal Size, int Quantity, decimal Price)
{
    public decimal Total => Quantity * Price;
}

public partial class MainWindow : Window
{
    private readonly StoreService store;
    private User? user;
    private readonly List<BasketRow> basket = [];
    private bool ready;
    private string CurrentView = "catalog";

    public MainWindow()
    {
        InitializeComponent();
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        store = new StoreService(DatabaseSettings.Load(AppContext.BaseDirectory));
        store.Initialize(Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql"), Path.Combine(AppContext.BaseDirectory, "Database", "seed.sql"));
        Logo.Source = new BitmapImage(new Uri(Asset("logo.png")));
        Icon = new BitmapImage(new Uri(Asset("store.ico")));
        CategoryBox.ItemsSource = new[] { "Все категории" }.Concat(store.Categories()).ToList();
        CategoryBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        ready = true;
        RefreshCatalog();
    }

    public static string Asset(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", filename);
        return File.Exists(path) ? path : Path.Combine(AppContext.BaseDirectory, "Assets", "picture.png");
    }

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception is Microsoft.Data.SqlClient.SqlException ? "Не удалось выполнить запрос к SQL Server. Проверьте подключение и повторите попытку.\n" + exception.Message : exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshCatalog()
    {
        if (!ready) return;
        var products = store.Products(user is null ? "" : SearchBox.Text, user is not null && CategoryBox.SelectedIndex > 0 ? CategoryBox.SelectedItem as string : null, user is null ? 0 : SortBox.SelectedIndex);
        ProductList.ItemsSource = products.Select(p => new ProductRow(p, Asset(Path.Combine("Images", p.Image)))).ToList();
        EmptyCatalog.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CurrentView == "catalog") StatusLabel.Text = $"Найдено моделей: {products.Count} из 31. Скидка 25% применяется, если в прошлом календарном месяце не было заказов модели.";
    }

    private void Navigate(string view)
    {
        CurrentView = view;
        CatalogPanel.Visibility = view == "catalog" ? Visibility.Visible : Visibility.Collapsed;
        CartPanel.Visibility = view == "cart" ? Visibility.Visible : Visibility.Collapsed;
        OrdersPanel.Visibility = view == "orders" ? Visibility.Visible : Visibility.Collapsed;
        Title = "Чудо Обувь: " + (view == "catalog" ? "каталог" : view == "cart" ? "оформление заказа" : "управление заказами");
        if (view == "catalog") RefreshCatalog();
        if (view == "cart") RefreshCart();
        if (view == "orders") RefreshOrders();
    }

    private void LoginClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var found = store.Login(LoginBox.Text);
        if (found is null) throw new InvalidOperationException("Логин не найден. Введите логин из списка пользователей системы. Пароль не требуется.");
        user = found;
        UserLabel.Text = user.FullName + " • " + (user.Role == Role.Administrator ? "Администратор" : user.Role == Role.Manager ? "Менеджер" : "Клиент");
        LoginBox.Visibility = LoginButton.Visibility = Visibility.Collapsed;
        LogoutButton.Visibility = Filters.Visibility = CartButton.Visibility = Visibility.Visible;
        OrdersButton.Visibility = CustomerPanel.Visibility = user.Role >= Role.Manager ? Visibility.Visible : Visibility.Collapsed;
        AdminControls.Visibility = user.Role == Role.Administrator ? Visibility.Visible : Visibility.Collapsed;
        if (user.Role >= Role.Manager)
        {
            var customers = store.Customers(user);
            CustomerBox.ItemsSource = customers;
            CustomerBox.SelectedItem = customers.First(c => c.Id == user.Id);
        }
        Navigate("catalog");
    });

    private void LoginKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) LoginClick(sender, e); }
    private void LogoutClick(object sender, RoutedEventArgs e)
    {
        if (basket.Count > 0 && !Confirm("Выйти и очистить несохранённую корзину?")) return;
        user = null;
        basket.Clear();
        CartButton.Content = "Корзина (0)";
        UserLabel.Text = "Гость";
        LoginBox.Text = "";
        LoginBox.Visibility = LoginButton.Visibility = Visibility.Visible;
        LogoutButton.Visibility = Filters.Visibility = CartButton.Visibility = OrdersButton.Visibility = CustomerPanel.Visibility = AdminControls.Visibility = Visibility.Collapsed;
        CustomerBox.ItemsSource = null;
        OrdersGrid.ItemsSource = OrderLinesGrid.ItemsSource = null;
        Navigate("catalog");
    }
    private void CatalogClick(object sender, RoutedEventArgs e) => Safe(() => Navigate("catalog"));
    private void CartClick(object sender, RoutedEventArgs e) => Safe(() => Navigate("cart"));
    private void OrdersClick(object sender, RoutedEventArgs e) => Safe(() => Navigate("orders"));
    private void SearchChanged(object sender, TextChangedEventArgs e) => Safe(RefreshCatalog);
    private void FilterChanged(object sender, SelectionChangedEventArgs e) => Safe(RefreshCatalog);
    private void DetailsClick(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is Product p) OpenDetails(p); }
    private void ProductDoubleClick(object sender, MouseButtonEventArgs e) { if (ProductList.SelectedItem is ProductRow row) OpenDetails(row.Product); }

    private void OpenDetails(Product product) => Safe(() =>
    {
        var fresh = store.Products().First(p => p.Id == product.Id);
        var dialog = new ProductWindow(fresh, store.Stock(product.Id), store.Stock(product.Id, false), user is not null) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedStock is not null)
        {
            var existing = basket.FirstOrDefault(b => b.StockItemId == dialog.SelectedStock.Id);
            var quantity = checked((existing?.Quantity ?? 0) + dialog.Quantity);
            if (quantity > dialog.SelectedStock.Quantity) throw new InvalidOperationException("С учётом корзины превышен остаток. Уменьшите количество.");
            if (existing is not null) basket.Remove(existing);
            basket.Add(new BasketRow(dialog.SelectedStock.Id, fresh.Name, dialog.SelectedStock.Size, quantity, fresh.Price));
            CartButton.Content = $"Корзина ({basket.Sum(b => b.Quantity)})";
            StatusLabel.Text = "Товар добавлен. Перейдите в корзину для подтверждения заказа.";
        }
    });

    private void RefreshCart()
    {
        // Обновляем цены перед подтверждением: календарный месяц мог измениться, пока корзина была открыта.
        var products = store.Products();
        for (var i = 0; i < basket.Count; i++)
        {
            var row = basket[i];
            var product = products.First(p => store.Stock(p.Id, false).Any(s => s.Id == row.StockItemId));
            basket[i] = row with { Price = product.Price };
        }
        CartGrid.ItemsSource = basket.ToList();
        CartTotal.Text = $"Итого: {basket.Sum(b => b.Total):N2} ₽";
        CartButton.Content = $"Корзина ({basket.Sum(b => b.Quantity)})";
        StatusLabel.Text = basket.Count == 0 ? "Корзина пуста. Выберите товары в каталоге." : "До подтверждения заказа остатки не резервируются. Выберите строку для изменения количества.";
    }

    private void UpdateCartClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (CartGrid.SelectedItem is not BasketRow row) throw new InvalidOperationException("Выберите позицию корзины.");
        if (!int.TryParse(CartQuantity.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("Введите положительное целое количество пар.");
        var stock = store.Products().SelectMany(p => store.Stock(p.Id, false)).First(s => s.Id == row.StockItemId);
        if (quantity > stock.Quantity) throw new InvalidOperationException($"Доступно {stock.Quantity} пар. Уменьшите количество.");
        basket[basket.IndexOf(row)] = row with { Quantity = quantity };
        RefreshCart();
    });
    private void RemoveCartClick(object sender, RoutedEventArgs e) => Safe(() => { if (CartGrid.SelectedItem is not BasketRow row) throw new InvalidOperationException("Выберите позицию корзины."); basket.Remove(row); RefreshCart(); });
    private bool Confirm(string message) => MessageBox.Show(this, message, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    private void ClearCartClick(object sender, RoutedEventArgs e) { if (Confirm("Очистить корзину и отказаться от несохранённого заказа?")) { basket.Clear(); RefreshCart(); } }
    private void ConfirmOrderClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (user is null) throw new InvalidOperationException("Войдите по логину.");
        if (basket.Count == 0) throw new InvalidOperationException("Корзина пуста. Добавьте хотя бы один товар.");
        RefreshCart();
        if (!Confirm($"Оформить заказ на сумму {basket.Sum(b => b.Total):N2} ₽?")) return;
        var id = store.PlaceOrder(user, basket.Select(b => new CartItem(b.StockItemId, b.Quantity)), (CustomerBox.SelectedItem as User)?.Id);
        basket.Clear();
        RefreshCart();
        Navigate("catalog");
        MessageBox.Show(this, $"Заказ №{id} сохранён. Остатки товаров обновлены.", "Заказ оформлен", MessageBoxButton.OK, MessageBoxImage.Information);
    });

    private void RefreshOrders(int? selectedId = null)
    {
        if (user is null) return;
        var orders = store.Orders(user);
        OrdersGrid.ItemsSource = orders;
        OrdersGrid.SelectedItem = orders.FirstOrDefault(o => o.Id == selectedId) ?? orders.FirstOrDefault();
        StatusLabel.Text = $"Заказов: {orders.Count}. Выберите заказ для просмотра состава.";
    }
    private void OrderSelectionChanged(object sender, SelectionChangedEventArgs e) => Safe(() =>
    {
        if (user is null || OrdersGrid.SelectedItem is not OrderSummary order) { OrderLinesGrid.ItemsSource = null; return; }
        OrderLinesGrid.ItemsSource = store.OrderLines(user, order.Id);
        OrderDate.SelectedDate = order.OrderedAt;
    });
    private OrderSummary SelectedOrder() => OrdersGrid.SelectedItem as OrderSummary ?? throw new InvalidOperationException("Выберите заказ в списке.");
    private void DeleteOrderClick(object sender, RoutedEventArgs e) => Safe(() => { var order = SelectedOrder(); if (!Confirm($"Удалить заказ №{order.Id}? Все его товары вернутся в доступные остатки.")) return; store.DeleteOrder(user!, order.Id); RefreshOrders(); });
    private void ChangeDateClick(object sender, RoutedEventArgs e) => Safe(() => { var order = SelectedOrder(); var date = OrderDate.SelectedDate ?? throw new InvalidOperationException("Выберите корректную дату."); store.ChangeOrderDate(user!, order.Id, date); RefreshOrders(order.Id); });
    private void DeleteLineClick(object sender, RoutedEventArgs e) => Safe(() => { var order = SelectedOrder(); if (OrderLinesGrid.SelectedItem is not OrderLine line) throw new InvalidOperationException("Выберите позицию заказа."); if (!Confirm("Удалить позицию и вернуть её количество в остаток?")) return; store.RemoveOrderLine(user!, order.Id, line.Id); RefreshOrders(order.Id); });
    private void NewOrderClick(object sender, RoutedEventArgs e) => Safe(() => { Navigate("catalog"); StatusLabel.Text = "Выберите товары, затем в корзине укажите клиента и подтвердите заказ."; });
}
