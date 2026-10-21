
### Запуск через Docker Compose
```powershell
# Клонирование репозитория
git clone https://github.com/Sanfran90/game-currency-shop.git
cd game-currency-shop/GameShopApi

# Сборка и запуск контейнеров
docker compose up -d --build

# Проверка работы
curl.exe http://localhost:8080/products


Для корректной отправки русских букв выполните перед запросами:
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

#Остановка
docker compose down          # Сохраняет данные БД
docker compose down -v       # Удаляет данные БД