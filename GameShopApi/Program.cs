using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=app;Username=postgres;Password=1234";

var dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// создать таблицу и засеять данными, если их ещё нет
await using (var conn = await dataSource.OpenConnectionAsync())
{
    await using var createCmd = conn.CreateCommand();
    createCmd.CommandText = """
        CREATE TABLE IF NOT EXISTS products (
            id SERIAL PRIMARY KEY,
            name TEXT NOT NULL,
            type TEXT NOT NULL,
            price NUMERIC(10,2) NOT NULL
        );
        """;
    await createCmd.ExecuteNonQueryAsync();

    await using var countCmd = conn.CreateCommand();
    countCmd.CommandText = "SELECT COUNT(*) FROM products;";
    var count = (long)(await countCmd.ExecuteScalarAsync())!;

    if (count == 0)
    {
        await using var seedCmd = conn.CreateCommand();
        seedCmd.CommandText = """
            INSERT INTO products (name, type, price) VALUES
            ('Золотые монеты x1000', 'currency', 4.99),
            ('Кристаллы x500', 'currency', 9.99),
            ('Пополнение баланса 10$', 'topup', 10.00);
            """;
        await seedCmd.ExecuteNonQueryAsync();
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// READ: список товаров
app.MapGet("/products", async (NpgsqlDataSource db) =>
{
    var products = new List<object>();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, type, price FROM products ORDER BY id;";
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        products.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            type = reader.GetString(2),
            price = reader.GetDecimal(3)
        });
    }
    return Results.Ok(products);
});

// READ: один товар
app.MapGet("/products/{id:int}", async (int id, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, name, type, price FROM products WHERE id = @id;";
    cmd.Parameters.AddWithValue("id", id);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Results.NotFound();
    return Results.Ok(new
    {
        id = reader.GetInt32(0),
        name = reader.GetString(1),
        type = reader.GetString(2),
        price = reader.GetDecimal(3)
    });
});

// CREATE
app.MapPost("/products", async (ProductInput input, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        INSERT INTO products (name, type, price)
        VALUES (@name, @type, @price)
        RETURNING id;
        """;
    cmd.Parameters.AddWithValue("name", input.Name);
    cmd.Parameters.AddWithValue("type", input.Type);
    cmd.Parameters.AddWithValue("price", input.Price);
    var newId = (int)(await cmd.ExecuteScalarAsync())!;
    return Results.Created($"/products/{newId}", new { id = newId, input.Name, input.Type, input.Price });
});

// UPDATE
app.MapPut("/products/{id:int}", async (int id, ProductInput input, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        UPDATE products SET name = @name, type = @type, price = @price
        WHERE id = @id;
        """;
    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("name", input.Name);
    cmd.Parameters.AddWithValue("type", input.Type);
    cmd.Parameters.AddWithValue("price", input.Price);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { id, input.Name, input.Type, input.Price });
});

// DELETE
app.MapDelete("/products/{id:int}", async (int id, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM products WHERE id = @id;";
    cmd.Parameters.AddWithValue("id", id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.NoContent();
});

app.Run();

record ProductInput(string Name, string Type, decimal Price);