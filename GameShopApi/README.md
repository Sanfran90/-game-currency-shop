
### 1. Склонировать репозиторий

```bash
git clone https://github.com/Sanfran90/-game-currency-shop.git
cd -game-currency-shop/GameShopApi
```

> На Windows в PowerShell/cmd переход в папку с дефисом в начале имени нужно делать через `cd ./-game-currency-shop/GameShopApi`.

### 2. Собрать Docker-образ

```bash
docker build -t game-shop-api .
```

### 3. Запустить контейнер

```bash
docker run -d --name game-shop-api -p 8080:8080 game-shop-api
```

### 4. Проверить, что контейнер работает

```bash
docker ps
```

В списке должен быть контейнер `game-shop-api` со статусом `Up` и портом `0.0.0.0:8080->8080/tcp`.

### 5. Открыть приложение

- Каталог товаров (API): [http://localhost:8080/products](http://localhost:8080/products)
- Проверка состояния: [http://localhost:8080/health](http://localhost:8080/health)
- Веб-интерфейс: [http://localhost:8080/](http://localhost:8080/)

Можно проверить через браузер, Postman или curl:

```bash
curl http://localhost:8080/products
```

### 6. Посмотреть логи (если что-то не работает)

```bash
docker logs game-shop-api
```

### 7. Остановить

```bash
docker stop game-shop-api
```

### Возможные проблемы

| Проблема | Решение |
|---|---|
| `port is already allocated` | Порт 8080 уже занят другим процессом — используйте другой порт, например `-p 8081:8080`, и открывайте `http://localhost:8081/` |
| `Cannot connect to the Docker daemon` | Docker Desktop не запущен — откройте приложение и дождитесь статуса "Engine running" |
| `no such file or directory: Dockerfile` | Вы не в той папке — убедитесь, что находитесь внутри `GameShopApi`, где лежит `Dockerfile` |