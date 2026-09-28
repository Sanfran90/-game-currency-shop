# Game Currency Shop API

API игрового магазина внутриигровой валюты на .NET 8, упакованное в Docker.

## Endpoints
- GET /products: каталог товаров
- GET /products/{id}: товар по id
- POST /orders: оформление заказа
- GET /orders/{id}: статус заказа
- GET /health: проверка состояния

## Локальный запуск (без Docker)
    dotnet run

## Сборка образа
    docker build -t game-shop-api .

## Запуск контейнера
    docker run -d --name game-shop-api -p 8080:8080 game-shop-api

## Проверка
    docker ps
    curl http://localhost:8080/products
    docker logs game-shop-api