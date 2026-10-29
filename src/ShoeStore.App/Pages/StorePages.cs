using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ShoeStore.App.Pages;

public partial class AuthorizationPage : Page
{
    private readonly Action<string> login;
    private readonly Action guest;
    public AuthorizationPage(Action<string> login, Action guest)
    {
        this.login = login;
        this.guest = guest;
        InitializeComponent();
        AuthLogo.Source = new BitmapImage(new Uri(MainWindow.Asset("logo.png")));
    }
    private void LoginClick(object sender, RoutedEventArgs e) => login(LoginField.Text);
    private void GuestClick(object sender, RoutedEventArgs e) => guest();
    private void LoginKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) login(LoginField.Text); }
}
