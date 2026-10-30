using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShoeStore.Core;

namespace ShoeStore.App;
public sealed class ProductWindow : Window
{
    public StockItem? SelectedStock { get; private set; }
    public int Quantity { get; private set; }

    public ProductWindow(Product product, List<StockItem> available, List<StockItem> allSizes, bool canOrder)
    {
        Title = "Чудо Обувь: " + product.Name;
        Icon = new BitmapImage(new Uri(MainWindow.Asset("store.ico")));
        Width = 760;
        Height = 760;
        MinWidth = 600;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel
        {
            Margin = new Thickness(24)
        };
        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Content = scroll;
        panel.Children.Add(new Image { Source = new BitmapImage(new Uri(MainWindow.Asset(System.IO.Path.Combine("Images", product.Image)))), Height = 200, Stretch = Stretch.Uniform });
        AddText(product.Name, 24, true);
        AddText(product.Category + " / " + product.Subcategory);
        AddText("Производство: " + product.Manufacturer);
        AddText(product.PriceLabel, 22, true);
        AddText("Состав: " + product.Composition);
        AddText("Описание: " + product.Description);
        AddText("Размерный ряд: " + string.Join(", ", allSizes.Select(s => s.Size.ToString("0.##"))));
        if (canOrder)
        {
            AddText("Доступный размер и количество пар");
            var sizes = new ComboBox
            {
                ItemsSource = available,
                DisplayMemberPath = "Label",
                SelectedIndex = available.Count > 0 ? 0 : -1
            };
            var quantity = new TextBox
            {
                Text = "1"
            };
            System.Windows.Automation.AutomationProperties.SetName(quantity, "Количество пар");
            var add = new Button
            {
                Content = available.Count > 0 ? "Добавить в заказ" : "Нет в наличии",
                IsEnabled = available.Count > 0,
                Background = new SolidColorBrush(Color.FromRgb(0x70, 0xB2, 0xAF))
            };
            add.Click += (_, _) =>
            {
                if (sizes.SelectedItem is not StockItem selected || !int.TryParse(quantity.Text, out var value) || value <= 0 || value > selected.Quantity)
                {
                    MessageBox.Show(this, "Выберите размер и введите положительное целое количество, не превышающее доступный остаток.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SelectedStock = selected;
                Quantity = value;
                DialogResult = true;
            };
            panel.Children.Add(sizes);
            panel.Children.Add(quantity);
            panel.Children.Add(add);
        }
        else
            AddText("Для оформления заказа войдите по логину в главном окне.");
        var back = new Button
        {
            Content = "← Назад",
            IsCancel = true
        };
        back.Click += (_, _) => Close();
        panel.Children.Add(back);
        void AddText(string text, double size = 16, bool bold = false) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(4, 5, 4, 5) });
    }
}
