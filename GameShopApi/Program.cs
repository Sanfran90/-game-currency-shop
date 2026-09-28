var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var products = new List<Product>
{
    new(1, "Золотые монеты x1000", "currency", 4.99m),
    new(2, "Кристаллы x500", "currency", 9.99m),
    new(3, "Пополнение баланса 10$", "topup", 10.00m)
};
var orders = new List<Order>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// каталог товаров
app.MapGet("/products", (ILogger<Program> log) =>
{
    log.LogInformation("GET /products: возвращено {Count} товаров", products.Count);
    return Results.Ok(products);
});

app.MapGet("/products/{id:int}", (int id, ILogger<Program> log) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);
    log.LogInformation("GET /products/{Id}: {Result}", id, product is null ? "не найден" : "найден");
    return product is null ? Results.NotFound() : Results.Ok(product);
});

// оформление заказа
app.MapPost("/orders", (CreateOrderRequest req, ILogger<Program> log) =>
{
    var product = products.FirstOrDefault(p => p.Id == req.ProductId);
    if (product is null || req.Quantity < 1)
        return Results.BadRequest(new { error = "Неверный товар или количество" });

    var order = new Order(orders.Count + 1, product.Id, req.Quantity, product.Price * req.Quantity, "created");
    orders.Add(order);
    log.LogInformation("POST /orders: создан заказ {Id} на сумму {Total}", order.Id, order.Total);
    return Results.Created($"/orders/{order.Id}", order);
});

// статус заказа
app.MapGet("/orders/{id:int}", (int id) =>
{
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

record Product(int Id, string Name, string Type, decimal Price);
record Order(int Id, int ProductId, int Quantity, decimal Total, string Status);
record CreateOrderRequest(int ProductId, int Quantity);