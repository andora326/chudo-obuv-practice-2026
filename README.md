# Чудо Обувь

Учебное WPF-приложение для каталога обуви и оформления заказов.

## Технологии

C#, WPF, .NET Framework 4.8, Entity Framework 6, SQL Server LocalDB.

## Запуск

1. Установите Visual Studio 2022 с компонентом **.NET Framework 4.8 Developer Pack**.
2. Создайте в SQL Server LocalDB базу `ShoeTest`.
3. Выполните скрипт [`database/full.sql`](database/full.sql).
4. Откройте [`ShoeStore.sln`](ShoeStore.sln) и запустите проект `ShoeStore.App`.


## Возможности

- каталог обуви с поиском, фильтрацией и сортировкой;
- вход по учебному логину без пароля;
- роли клиента, менеджера и администратора;
- выбор размера и количества;
- корзина и оформление заказа;
- проверка остатков и возврат товара при удалении заказа;
- просмотр заказов и операции по роли.

## Учебные пользователи

Логины находятся в [`database/seed.sql`](database/seed.sql). Пароль не требуется.

## Подключение к базе

По умолчанию используется `(localdb)\\MSSQLLocalDB` и база `ShoeTest`. Для другой базы измените `release/final/connection-string.txt`.

Проект рассчитан на Windows. В Linux-среде WPF, .NET Framework 4.8 и SQL Server LocalDB не запускаются.
