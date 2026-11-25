using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

public record Cred(string Login, string Password);
public record Item(string Name, decimal Price, int Qty);
public record PayReq(string Method, List<Item> Items);
public record PassReq(string OldPassword, string NewPassword);
public record RenameReq(string Login);

public static class Mvp
{
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    static NpgsqlCommand Q(NpgsqlDataSource db, string sql, params (string, object)[] p)
    {
        var c = db.CreateCommand(sql);
        foreach (var (k, v) in p) c.Parameters.AddWithValue(k, v);
        return c;
    }

    static string Hash(string pw)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(pw, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(h);
    }

    static bool Check(string pw, string stored)
    {
        var p = stored.Split(':');
        var h = Rfc2898DeriveBytes.Pbkdf2(pw, Convert.FromBase64String(p[0]), 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(p[1]));
    }

    static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    static async Task<(int id, string login)?> Auth(NpgsqlDataSource db, HttpRequest r)
    {
        var t = r.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (t == "") return null;
        await using var cmd = Q(db, "SELECT id, login FROM users WHERE token = @t", ("t", t));
        await using var rd = await cmd.ExecuteReaderAsync();
        return await rd.ReadAsync() ? (rd.GetInt32(0), rd.GetString(1)) : null;
    }

    public static async Task InitAsync(NpgsqlDataSource db)
    {
        await using var cmd = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                login TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                token TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS purchases (
                id SERIAL PRIMARY KEY,
                user_id INT NOT NULL REFERENCES users(id),
                items JSONB NOT NULL,
                total NUMERIC(12,2) NOT NULL,
                method TEXT NOT NULL,
                paid_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            INSERT INTO products (name, type, price)
            SELECT * FROM (VALUES
                ('Genshin Impact: 330 кристаллов', 'currency', 4.99),
                ('Honkai: Star Rail: 300 нефрита', 'currency', 4.99),
                ('Fortnite: 1000 V-Bucks', 'currency', 8.99),
                ('Roblox: 800 Robux', 'currency', 9.99),
                ('PUBG Mobile: 660 UC', 'currency', 9.99),
                ('Valorant: 1000 VP', 'currency', 9.99),
                ('Dota 2: Battle Pass', 'pass', 9.99),
                ('Counter-Strike 2: Prime', 'pass', 14.99),
                ('Minecraft: 1720 монет', 'currency', 14.99),
                ('World of Tanks: 1000 золота', 'currency', 7.99)
            ) AS v(name, type, price)
            WHERE (SELECT COUNT(*) FROM products) < 6;
            """);
        await cmd.ExecuteNonQueryAsync();
    }

    public static void MapMvp(this WebApplication app)
    {
        var db = app.Services.GetRequiredService<NpgsqlDataSource>();

        app.MapPost("/api/register", async (Cred req) =>
        {
            var login = req.Login.Trim();
            if (login.Length < 3 || req.Password.Length < 4)
                return Results.BadRequest(new { error = "Логин от 3 символов, пароль от 4" });
            var token = NewToken();
            await using var cmd = Q(db,
                "INSERT INTO users (login, password_hash, token) VALUES (@l, @p, @t) ON CONFLICT (login) DO NOTHING RETURNING id",
                ("l", login), ("p", Hash(req.Password)), ("t", token));
            if (await cmd.ExecuteScalarAsync() is null)
                return Results.Conflict(new { error = "Этот логин уже занят" });
            return Results.Ok(new { token, login });
        });

        app.MapPost("/api/login", async (Cred req) =>
        {
            var login = req.Login.Trim();
            int id; string hash;
            await using (var cmd = Q(db, "SELECT id, password_hash FROM users WHERE login = @l", ("l", login)))
            await using (var rd = await cmd.ExecuteReaderAsync())
            {
                if (!await rd.ReadAsync()) return Results.Json(new { error = "Неверный логин или пароль" }, statusCode: 401);
                id = rd.GetInt32(0); hash = rd.GetString(1);
            }
            if (!Check(req.Password, hash)) return Results.Json(new { error = "Неверный логин или пароль" }, statusCode: 401);
            var token = NewToken();
            await using var upd = Q(db, "UPDATE users SET token = @t WHERE id = @id", ("t", token), ("id", id));
            await upd.ExecuteNonQueryAsync();
            return Results.Ok(new { token, login });
        });

        // имитация оплаты: вызывается после 5-секундного таймера на странице
        app.MapPost("/api/pay", async (HttpRequest r, PayReq req) =>
        {
            var u = await Auth(db, r);
            if (u is null) return Results.Unauthorized();
            if (req.Items.Count == 0 || req.Items.Any(i => i.Price <= 0 || i.Qty < 1 || i.Qty > 99))
                return Results.BadRequest(new { error = "Корзина пуста или заполнена неверно" });
            var total = req.Items.Sum(i => i.Price * i.Qty);
            await using var cmd = Q(db,
                "INSERT INTO purchases (user_id, items, total, method) VALUES (@u, @i::jsonb, @t, @m) RETURNING id",
                ("u", u.Value.id), ("i", JsonSerializer.Serialize(req.Items, J)), ("t", total), ("m", req.Method));
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id, total });
        });

        app.MapGet("/api/purchases", async (HttpRequest r) =>
        {
            var u = await Auth(db, r);
            if (u is null) return Results.Unauthorized();
            var list = new List<object>();
            await using var cmd = Q(db,
                "SELECT id, items::text, total, method, paid_at FROM purchases WHERE user_id = @u ORDER BY id DESC",
                ("u", u.Value.id));
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new
                {
                    id = rd.GetInt32(0),
                    items = JsonDocument.Parse(rd.GetString(1)).RootElement,
                    total = rd.GetDecimal(2),
                    method = rd.GetString(3),
                    paidAt = rd.GetDateTime(4)
                });
            return Results.Ok(list);
        });

        app.MapGet("/api/me", async (HttpRequest r) =>
        {
            var u = await Auth(db, r);
            if (u is null) return Results.Unauthorized();
            await using var cmd = Q(db,
                "SELECT u.created_at, COUNT(p.id), COALESCE(SUM(p.total), 0) FROM users u LEFT JOIN purchases p ON p.user_id = u.id WHERE u.id = @u GROUP BY u.id",
                ("u", u.Value.id));
            await using var rd = await cmd.ExecuteReaderAsync();
            await rd.ReadAsync();
            return Results.Ok(new { login = u.Value.login, createdAt = rd.GetDateTime(0), orders = rd.GetInt64(1), spent = rd.GetDecimal(2) });
        });

        app.MapPost("/api/password", async (HttpRequest r, PassReq req) =>
        {
            var u = await Auth(db, r);
            if (u is null) return Results.Unauthorized();
            if (req.NewPassword.Length < 4) return Results.BadRequest(new { error = "Новый пароль от 4 символов" });
            string hash;
            await using (var cmd = Q(db, "SELECT password_hash FROM users WHERE id = @id", ("id", u.Value.id)))
                hash = (string)(await cmd.ExecuteScalarAsync())!;
            if (!Check(req.OldPassword, hash)) return Results.BadRequest(new { error = "Старый пароль неверный" });
            await using var upd = Q(db, "UPDATE users SET password_hash = @p WHERE id = @id", ("p", Hash(req.NewPassword)), ("id", u.Value.id));
            await upd.ExecuteNonQueryAsync();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/rename", async (HttpRequest r, RenameReq req) =>
        {
            var u = await Auth(db, r);
            if (u is null) return Results.Unauthorized();
            var login = req.Login.Trim();
            if (login.Length < 3) return Results.BadRequest(new { error = "Логин от 3 символов" });
            try
            {
                await using var cmd = Q(db, "UPDATE users SET login = @l WHERE id = @id", ("l", login), ("id", u.Value.id));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (PostgresException e) when (e.SqlState == "23505")
            {
                return Results.Conflict(new { error = "Этот логин уже занят" });
            }
            return Results.Ok(new { login });
        });
    }
}
