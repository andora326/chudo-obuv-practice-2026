using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShoeStore.App.Pages;

public partial class ProductsPage : Page
{
    private readonly MainWindow owner;
    public ProductsPage(MainWindow owner)
    {
        this.owner = owner;
        InitializeComponent();
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) => owner.SearchChanged(sender, e);
    private void FilterChanged(object sender, SelectionChangedEventArgs e) => owner.FilterChanged(sender, e);
    private void ProductDoubleClick(object sender, MouseButtonEventArgs e) => owner.ProductDoubleClick(sender, e);
    private void DetailsClick(object sender, RoutedEventArgs e) => owner.DetailsClick(sender, e);
}
