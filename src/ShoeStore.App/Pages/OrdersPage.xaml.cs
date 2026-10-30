using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShoeStore.App.Pages;
public partial class OrdersPage : Page
{
    private readonly MainWindow owner;
    public OrdersPage(MainWindow owner)
    {
        this.owner = owner;
        InitializeComponent();
    }

    private void NewOrderClick(object sender, RoutedEventArgs e) => owner.NewOrderClick(sender, e);
    private void OrderSelectionChanged(object sender, SelectionChangedEventArgs e) => owner.OrderSelectionChanged(sender, e);
    private void DeleteOrderClick(object sender, RoutedEventArgs e) => owner.DeleteOrderClick(sender, e);
    private void ChangeDateClick(object sender, RoutedEventArgs e) => owner.ChangeDateClick(sender, e);
    private void DeleteLineClick(object sender, RoutedEventArgs e) => owner.DeleteLineClick(sender, e);
}
