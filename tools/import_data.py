"""Преобразование официальных XLSX в воспроизводимый SQL без изменения исходных остатков."""
from pathlib import Path
from decimal import Decimal
import json
import shutil
import sqlite3
import openpyxl

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parent / 'materials' / 'data'

def rows(name):
    return [r for r in list(openpyxl.load_workbook(SOURCE / name, data_only=True).active.values)[1:] if r[0] is not None]

def quote(value):
    return str(value) if isinstance(value, int) else "'" + str(value).replace("'", "''") + "'"

def clean(value):
    return ' '.join(str(value).split())

def product_key(name, manufacturer):
    name = clean(name)
    if name == 'Черные туфли в классическом стиле':
        name = 'Черные туфли в классическом стиле — база для деловых образов'
    return name, clean(manufacturer)

sql = []
def insert(table, *values):
    sql.append(f"INSERT INTO {table} VALUES({','.join(map(quote, values))});")

roles = {'Авторизованный пользователь': 1, 'Менеджер': 2, 'Администратор': 3}
for label, id in roles.items():
    insert('Roles', id, label)
users = {}
for id, (last, first, middle, login, role) in enumerate(rows('Users_import.xlsx'), 1):
    insert('Users', id, login, last, first, middle, roles[role])
    users[f'{last} {first} {middle}'] = id

categories, subcategories, manufacturers, products, sizes, stocks = {}, {}, {}, {}, {}, {}
for id, (category, subcategory, image, name, manufacturer, description, composition, price) in enumerate(rows('Products_import.xlsx'), 1):
    if category not in categories:
        categories[category] = len(categories) + 1
        insert('Categories', categories[category], category)
    key = (category, subcategory)
    if key not in subcategories:
        subcategories[key] = len(subcategories) + 1
        insert('Subcategories', subcategories[key], categories[category], subcategory)
    if manufacturer not in manufacturers:
        manufacturers[manufacturer] = len(manufacturers) + 1
        insert('Manufacturers', manufacturers[manufacturer], manufacturer)
    insert('Products', id, subcategories[key], manufacturers[manufacturer], name, image or 'picture.png', description or '', composition or '', int(Decimal(str(price))*100))
    products[product_key(name, manufacturer)] = id

for id, (size,) in enumerate(rows('Sizes_import.xlsx'), 1):
    sizes[float(size)] = id
    insert('Sizes', id, float(size))

for id, (name, manufacturer, size, quantity) in enumerate(rows('Stock_Items_import.xlsx'), 1):
    key = (*product_key(name, manufacturer), float(size))
    assert key not in stocks, key
    stocks[key] = id
    insert('StockItems', id, products[product_key(name, manufacturer)], sizes[float(size)], int(quantity))

orders = set()
missing = []
for lineId, (order, date, customer, category, name, manufacturer, size, quantity, price) in enumerate(rows('Orders_import.xlsx'), 1):
    if order not in orders:
        insert('Orders', int(order), date.strftime('%Y-%m-%d'), users[customer])
        orders.add(order)
    key = (*product_key(name, manufacturer), float(size))
    if key not in stocks:
        stocks[key] = len(stocks) + 1
        insert('StockItems', stocks[key], products[product_key(name, manufacturer)], sizes[float(size)], 0)
        missing.append(key)
    insert('OrderLines', lineId, int(order), stocks[key], int(quantity), int(Decimal(str(price))*100))

(ROOT / 'database' / 'seed.sql').write_text('\n'.join(sql)+'\n', encoding='utf-8')
con = sqlite3.connect(':memory:')
con.executescript((ROOT / 'database' / 'schema.sql').read_text(encoding='utf-8'))
con.executescript('\n'.join(sql))
assert not list(con.execute('PRAGMA foreign_key_check'))
assets = ROOT / 'src' / 'ShoeStore.App' / 'Assets'
shutil.copytree(SOURCE / 'Images', assets / 'Images', dirs_exist_ok=True)
for source, dest in [('picture.png','picture.png'),('Чудо Обувь.png','logo.png'),('Чудо Обувь.ico','store.ico')]:
    shutil.copy2(SOURCE / source, assets / dest)
summary = {t: con.execute(f'SELECT COUNT(*) FROM {t}').fetchone()[0] for t in ['Users','Products','StockItems','Orders','OrderLines','Sizes']}
summary['historical_positions_added_with_zero_stock'] = missing
summary['stock_import_policy'] = 'Исходные количества доступны для новых заказов. Исторические заказы повторно их не уменьшают.'
(ROOT / 'docs' / 'import-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False, indent=2))
