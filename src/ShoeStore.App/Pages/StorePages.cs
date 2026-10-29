using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShoeStore.App.Pages;

public sealed class ProductsPage : Page
{
    public ProductsPage(UIElement content) { Title = "Каталог обуви"; Content = content; }
}
public sealed class CartPage : Page
{
    public CartPage(UIElement content) { Title = "Оформление заказа"; Content = content; }
}
public sealed class OrdersPage : Page
{
    public OrdersPage(UIElement content) { Title = "Заказы клиентов"; Content = content; }
}
public sealed class AuthorizationPage : Page
{
    public AuthorizationPage(Action<string> login, Action guest)
    {
        Title = "Авторизация";
        var panel = new StackPanel { Width = 420, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "Вход в систему", FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 4, 16) });
        panel.Children.Add(new TextBlock { Text = "Введите логин из списка пользователей. Пароль не требуется.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 10) });
        var input = new TextBox { ToolTip = "Логин пользователя" };
        System.Windows.Automation.AutomationProperties.SetName(input, "Логин пользователя");
        var enter = new Button { Content = "Войти", Background = new SolidColorBrush(Color.FromRgb(0x70, 0xB2, 0xAF)) };
        enter.Click += (_, _) => login(input.Text);
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) login(input.Text); };
        var open = new Button { Content = "Войти как гость" };
        open.Click += (_, _) => guest();
        panel.Children.Add(input);
        panel.Children.Add(enter);
        panel.Children.Add(open);
        Content = panel;
    }
}
