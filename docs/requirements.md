# Требования и соответствие реализации

| Требование | Где реализовано |
|---|---|
| Компания, каталог и карточка товара | MainWindow.xaml, ProductWindow.cs |
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
| База в 3НФ, ограничения целостности | database/schema.sql |
| Импорт и воспроизводимый скрипт с данными | seed.sql, full.sql, import_data.py |
| Ошибки, предупреждения, подтверждения | Safe, Confirm, ProductWindow |

## Границы решения

Microsoft SQL Server Express LocalDB используется для локального запуска; подключение к другому экземпляру SQL Server задаётся в connection-string.txt. Все изменения заказа выполняются в транзакциях. Роль перепроверяется в базе внутри сервиса, поэтому скрытие кнопки не является единственной проверкой доступа. Модель предназначена для локальной учебной системы.

Даты практики в отчёте: 19.10.2026–07.11.2026 по указанию обучающегося. Поля подписей, оценок, печатей и данных наставника не заполняются автоматически.
