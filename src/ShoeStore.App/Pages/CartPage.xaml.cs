using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShoeStore.App.Pages;

public partial class CartPage : Page
{
    private readonly MainWindow owner;
    public CartPage(MainWindow owner)
    {
        this.owner = owner;
        InitializeComponent();
    }
    private void UpdateCartClick(object sender, RoutedEventArgs e) => owner.UpdateCartClick(sender, e);
    private void RemoveCartClick(object sender, RoutedEventArgs e) => owner.RemoveCartClick(sender, e);
    private void ClearCartClick(object sender, RoutedEventArgs e) => owner.ClearCartClick(sender, e);
    private void ConfirmOrderClick(object sender, RoutedEventArgs e) => owner.ConfirmOrderClick(sender, e);
}
