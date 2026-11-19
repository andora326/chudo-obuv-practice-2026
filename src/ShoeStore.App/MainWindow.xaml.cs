using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ShoeStore.Core;
using ShoeStore.App.Pages;
using System.Windows.Navigation;

namespace ShoeStore.App;
public sealed class ProductRow
{
    public Product Product { get; set; }
    public string ImagePath { get; set; }

    public ProductRow(Product product, string imagePath)
    {
        Product = product;
        ImagePath = imagePath;
    }

    public bool LowStock
    {
        get { return Product.Quantity <= 3; }
    }

    public string Title => $"{Product.Manufacturer} | {Product.Name}";
    public string CategoryLabel => $"Категория {Product.Category}";
    public string QuantityLabel => $"Количество {Product.Availability}";
}

public sealed class BasketRow
{
    public int StockItemId { get; set; }
    public string Name { get; set; }
    public decimal Size { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }

    public BasketRow(int stockItemId, string name, decimal size, int quantity, decimal price)
    {
        StockItemId = stockItemId;
        Name = name;
        Size = size;
        Quantity = quantity;
        Price = price;
    }

    public decimal Total
    {
        get { return Quantity * Price; }
    }
}

public partial class MainWindow : Window
{
    private readonly StoreService store;
    private ProductsPage productsPage = null !;
    private CartPage cartPage = null !;
    private OrdersPage ordersPage = null !;
    private Grid CatalogPanel => productsPage.CatalogPanel;
    private Grid Filters => productsPage.Filters;
    private TextBox SearchBox => productsPage.SearchBox;
    private ComboBox CategoryBox => productsPage.CategoryBox;
    private ComboBox SortBox => productsPage.SortBox;
    private ListView ProductList => productsPage.ProductList;
    private TextBlock EmptyCatalog => productsPage.EmptyCatalog;
    private Grid CartPanel => cartPage.CartPanel;
    private StackPanel CustomerPanel => cartPage.CustomerPanel;
    private ComboBox CustomerBox => cartPage.CustomerBox;
    private DataGrid CartGrid => cartPage.CartGrid;
    private TextBox CartQuantity => cartPage.CartQuantity;
    private TextBlock CartTotal => cartPage.CartTotal;
    private Grid OrdersPanel => ordersPage.OrdersPanel;
    private DataGrid OrdersGrid => ordersPage.OrdersGrid;
    private DataGrid OrderLinesGrid => ordersPage.OrderLinesGrid;
    private StackPanel AdminControls => ordersPage.AdminControls;
    private DatePicker OrderDate => ordersPage.OrderDate;

    private readonly Frame mainFrame = new Frame
    {
        NavigationUIVisibility = NavigationUIVisibility.Hidden,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    private readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
    private User? user;
    private readonly List<BasketRow> basket = [];
    private bool ready;
    private string CurrentView = "catalog";
    public MainWindow()
    {
        InitializeComponent();
        productsPage = new ProductsPage(this);
        pages["catalog"] = productsPage;
        cartPage = new CartPage(this);
        pages["cart"] = cartPage;
        ordersPage = new OrdersPage(this);
        pages["orders"] = ordersPage;
        pages["auth"] = new AuthorizationPage(Login, () => Navigate("catalog"));
        PagesHost.Children.Add(mainFrame);
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        store = new StoreService(DatabaseSettings.Load(AppContext.BaseDirectory));
        store.CheckConnection();
        Logo.Source = new BitmapImage(new Uri(Asset("logo.png")));
        Icon = new BitmapImage(new Uri(Asset("store.ico")));
        CategoryBox.ItemsSource = new[]
        {
            "Все категории"
        }.Concat(store.Categories()).ToList();
        CategoryBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        ready = true;
        Navigate("auth");
    }

    public static string Asset(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", filename);
        return File.Exists(path) ? path : Path.Combine(AppContext.BaseDirectory, "Assets", "picture.png");
    }

    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception is System.Data.SqlClient.SqlException ? "Не удалось выполнить запрос к SQL Server. Проверьте подключение и повторите попытку.\n" + exception.Message : exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshCatalog()
    {
        if (!ready)
            return;
        var products = store.Products(user is null ? "" : SearchBox.Text, user is not null && CategoryBox.SelectedIndex > 0 ? CategoryBox.SelectedItem as string : null, user is null ? 0 : SortBox.SelectedIndex);
        ProductList.ItemsSource = products.Select(p => new ProductRow(p, Asset(Path.Combine("Images", p.Image)))).ToList();
        EmptyCatalog.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CurrentView == "catalog")
            StatusLabel.Text = $"Найдено моделей: {products.Count} из 31. Скидка 25% применяется, если в прошлом календарном месяце не было заказов модели.";
    }

    private void Navigate(string view)
    {
        CurrentView = view;
        var isGuest = user is null;
        if (isGuest)
            UserLabel.Text = view == "auth" ? "Пользователь:" : "Гость";
        ActionBar.Visibility = view == "auth" ? Visibility.Collapsed : Visibility.Visible;
        NavigationBar.Visibility = user is null ? Visibility.Collapsed : Visibility.Visible;
        CartButton.Visibility = user is null ? Visibility.Collapsed : Visibility.Visible;
        OrdersButton.Visibility = user is not null && user.Role >= Role.Manager ? Visibility.Visible : Visibility.Collapsed;
        LogoutButton.Visibility = user is null ? Visibility.Collapsed : Visibility.Visible;
        ExitButton.Visibility = user is null && view != "auth" ? Visibility.Visible : Visibility.Collapsed;
        mainFrame.Navigate(pages[view]);
        if (view == "catalog")
            RefreshCatalog();
        if (view == "cart")
            RefreshCart();
        if (view == "orders")
            RefreshOrders();
    }

    private void Login(string login) => Safe(() =>
    {
        var found = store.Login(login);
        if (found is null)
            throw new InvalidOperationException("Логин не найден. Введите логин из списка пользователей системы. Пароль не требуется.");
        user = found;
        UserLabel.Text = user.FullName + " • " + (user.Role == Role.Administrator ? "Администратор" : user.Role == Role.Manager ? "Менеджер" : "Клиент");
        Filters.Visibility = Visibility.Visible;
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

    private void AuthClick(object sender, RoutedEventArgs e)
    {
        if (user is null)
            Navigate("auth");
    }

    private void LogoutClick(object sender, RoutedEventArgs e)
    {
        if (basket.Count > 0 && !Confirm("Выйти и очистить несохранённую корзину?"))
            return;
        user = null;
        basket.Clear();
        CartButton.Content = "Корзина (0)";
        UserLabel.Text = "Гость";
        LogoutButton.Visibility = Filters.Visibility = CartButton.Visibility = OrdersButton.Visibility = CustomerPanel.Visibility = AdminControls.Visibility = Visibility.Collapsed;
        CustomerBox.ItemsSource = null;
        OrdersGrid.ItemsSource = OrderLinesGrid.ItemsSource = null;
        Navigate("auth");
    }

    private void ExitClick(object sender, RoutedEventArgs e) => Navigate("auth");

    private void CatalogClick(object sender, RoutedEventArgs e) => Safe(() => Navigate("catalog"));
    private void CartClick(object sender, RoutedEventArgs e) => Safe(() => Navigate("cart"));
    private void OrdersClick(object sender, RoutedEventArgs e) => Safe(() => Navigate("orders"));
    internal void SearchChanged(object sender, TextChangedEventArgs e) => Safe(RefreshCatalog);
    internal void FilterChanged(object sender, SelectionChangedEventArgs e) => Safe(RefreshCatalog);
    internal void DetailsClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is Product p)
            OpenDetails(p);
    }

    internal void ProductDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProductList.SelectedItem is ProductRow row)
            OpenDetails(row.Product);
    }

    private void OpenDetails(Product product) => Safe(() =>
    {
        var fresh = store.Products().First(p => p.Id == product.Id);
        var dialog = new ProductWindow(fresh, store.Stock(product.Id), store.Stock(product.Id, false), user is not null)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.SelectedStock is not null)
        {
            var existing = basket.FirstOrDefault(b => b.StockItemId == dialog.SelectedStock.Id);
            var quantity = checked((existing?.Quantity ?? 0) + dialog.Quantity);
            if (quantity > dialog.SelectedStock.Quantity)
                throw new InvalidOperationException("С учётом корзины превышен остаток. Уменьшите количество.");
            if (existing is not null)
                basket.Remove(existing);
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
            row.Price = product.Price;
        }

        CartGrid.ItemsSource = basket.ToList();
        CartTotal.Text = $"Итого: {basket.Sum(b => b.Total):N2} ₽";
        CartButton.Content = $"Корзина ({basket.Sum(b => b.Quantity)})";
        StatusLabel.Text = basket.Count == 0 ? "Корзина пуста. Выберите товары в каталоге." : "До подтверждения заказа остатки не резервируются. Выберите строку для изменения количества.";
    }

    internal void UpdateCartClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (CartGrid.SelectedItem is not BasketRow row)
            throw new InvalidOperationException("Выберите позицию корзины.");
        if (!int.TryParse(CartQuantity.Text, out var quantity) || quantity <= 0)
            throw new InvalidOperationException("Введите положительное целое количество пар.");
        var stock = store.Products().SelectMany(p => store.Stock(p.Id, false)).First(s => s.Id == row.StockItemId);
        if (quantity > stock.Quantity)
            throw new InvalidOperationException($"Доступно {stock.Quantity} пар. Уменьшите количество.");
        row.Quantity = quantity;
        RefreshCart();
    });
    internal void RemoveCartClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (CartGrid.SelectedItem is not BasketRow row)
            throw new InvalidOperationException("Выберите позицию корзины.");
        basket.Remove(row);
        RefreshCart();
    });
    private bool Confirm(string message) => MessageBox.Show(this, message, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    internal void ClearCartClick(object sender, RoutedEventArgs e)
    {
        if (Confirm("Очистить корзину и отказаться от несохранённого заказа?"))
        {
            basket.Clear();
            RefreshCart();
        }
    }

    internal void ConfirmOrderClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (user is null)
            throw new InvalidOperationException("Войдите по логину.");
        if (basket.Count == 0)
            throw new InvalidOperationException("Корзина пуста. Добавьте хотя бы один товар.");
        RefreshCart();
        if (!Confirm($"Оформить заказ на сумму {basket.Sum(b => b.Total):N2} ₽?"))
            return;
        var id = store.PlaceOrder(user, basket.Select(b => new CartItem(b.StockItemId, b.Quantity)), (CustomerBox.SelectedItem as User)?.Id);
        basket.Clear();
        RefreshCart();
        Navigate("catalog");
        MessageBox.Show(this, $"Заказ №{id} сохранён. Остатки товаров обновлены.", "Заказ оформлен", MessageBoxButton.OK, MessageBoxImage.Information);
    });
    private void RefreshOrders(int? selectedId = null)
    {
        if (user is null)
            return;
        var orders = store.Orders(user);
        OrdersGrid.ItemsSource = orders;
        OrdersGrid.SelectedItem = orders.FirstOrDefault(o => o.Id == selectedId) ?? orders.FirstOrDefault();
        StatusLabel.Text = $"Заказов: {orders.Count}. Выберите заказ для просмотра состава.";
    }

    internal void OrderSelectionChanged(object sender, SelectionChangedEventArgs e) => Safe(() =>
    {
        if (user is null || OrdersGrid.SelectedItem is not OrderSummary order)
        {
            OrderLinesGrid.ItemsSource = null;
            return;
        }

        OrderLinesGrid.ItemsSource = store.OrderLines(user, order.Id);
        OrderDate.SelectedDate = order.OrderedAt;
    });
    private OrderSummary SelectedOrder() => OrdersGrid.SelectedItem as OrderSummary ?? throw new InvalidOperationException("Выберите заказ в списке.");
    internal void DeleteOrderClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var order = SelectedOrder();
        if (!Confirm($"Удалить заказ №{order.Id}? Все его товары вернутся в доступные остатки."))
            return;
        store.DeleteOrder(user!, order.Id);
        RefreshOrders();
    });
    internal void ChangeDateClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var order = SelectedOrder();
        var date = OrderDate.SelectedDate ?? throw new InvalidOperationException("Выберите корректную дату.");
        store.ChangeOrderDate(user!, order.Id, date);
        RefreshOrders(order.Id);
    });
    internal void DeleteLineClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var order = SelectedOrder();
        if (OrderLinesGrid.SelectedItem is not OrderLine line)
            throw new InvalidOperationException("Выберите позицию заказа.");
        if (!Confirm("Удалить позицию и вернуть её количество в остаток?"))
            return;
        store.RemoveOrderLine(user!, order.Id, line.Id);
        RefreshOrders(order.Id);
    });
    internal void NewOrderClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        Navigate("catalog");
        StatusLabel.Text = "Выберите товары, затем в корзине укажите клиента и подтвердите заказ.";
    });
}
