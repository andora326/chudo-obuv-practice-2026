# Требования и соответствие реализации

| Требование | Где реализовано |
|---|---|
| Компания, каталог и карточка товара | Pages/ProductsPage.xaml, ProductWindow.cs |
| Calibri, белый фон, #D2F6E7, #70B2AF, логотип и иконка | App.xaml, Assets |
| Остаток ≤3 — #FF8080, много >5 / мало ≤5 | ProductRow.LowStock, Product.Availability |
| Скидка по предыдущему календарному месяцу | StoreService.Products |
| Вход по логину, ФИО справа | StoreService.Login, MainWindow.LoginClick |
| Поиск, категории, сортировка вместе | StoreService.Products, Filters |
| Выбор размера и количества | ProductWindow, StoreService.Stock |
| Корзина и отказ до подтверждения | CartPanel, basket |
| Атомарное оформление и обновление остатков | StoreService.PlaceOrder |
| Список и состав заказов | StoreService.Orders, OrderLines |
| Менеджер создаёт заказ клиенту и удаляет заказы | PlaceOrder, DeleteOrder |
| Администратор редактирует дату и строки | ChangeOrderDate, RemoveOrderLine |
| Справочники, ключи и ссылочная целостность | database/schema.sql; сверка SSDL и рабочей базы |
| Учебные данные для локальной базы | seed.sql, full.sql |
| Ошибки, предупреждения, подтверждения | Safe, Confirm, ProductWindow |

## Границы решения

Microsoft SQL Server Express LocalDB используется для локального запуска; подключение к другому экземпляру SQL Server задаётся в connection-string.txt. Все изменения заказа выполняются в транзакциях. Роль перепроверяется в базе внутри сервиса, поэтому скрытие кнопки не является единственной проверкой доступа. Модель предназначена для локальной учебной системы.

## Технология по учебному видео

.NET Framework 4.8, WPF, Entity Framework 6, EDMX Database First. Каталог формируется LINQ-запросом к DbSet, без написания SQL для вывода. Используются Frame/Page, ListView/DataTemplate и Binding. Источник: часть 3 (05:53, 07:19, 08:48) и часть 4 (10:36, 11:09, 31:13). Структура учитывает исправления второй части: OrderItems ссылается на StockItems, отдельная цена строки удалена, imagePath допускает NULL. В Products есть самостоятельные ссылки на Categories и Subcategories; price имеет тип decimal(18,0), Sizes.size имеет тип float.

## Подтверждение

Повторная проверка 30.10.2026: [протокол](verification.md), [29 успешных сценариев](test-results.txt). Отдельно проверен full.sql на пустой базе с сопоставлением всех учебных данных. OrderLines и RemoveOrderLine — имена метода и модели интерфейса; фактическая таблица называется OrderItems.
