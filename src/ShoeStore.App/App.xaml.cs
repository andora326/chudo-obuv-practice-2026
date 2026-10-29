using System.Windows;

namespace ShoeStore.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Не удалось выполнить действие. Проверьте введённые данные и повторите попытку.\n\n" + (args.Exception.InnerException?.Message ?? args.Exception.Message), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
