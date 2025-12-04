using EfCoreTest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString(nameof(TestDatabase))
    ?? throw new InvalidOperationException($"Connection string '{nameof(TestDatabase)}' not found.");

var options = new DbContextOptionsBuilder<TestDatabase>()
    .UseSqlServer(connectionString)
    .Options;

using var db = new TestDatabase(options);

Console.WriteLine("Deleting database if it exists...");
await db.Database.EnsureDeletedAsync();

Console.WriteLine("Creating new database...");
await db.Database.EnsureCreatedAsync();

Console.WriteLine("Generating test data...");

var random = new Random(42);
var statuses = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

// Generate 50000 products
Console.WriteLine("Creating 50000 products...");
var products = new List<Product>(50000);
for (int i = 1; i <= 50000; i++)
{
    products.Add(new Product
    {
        Name = $"Product {i}",
        Description = $"Description for product {i}. This is a sample product used for testing purposes.",
        Price = Math.Round((decimal)(random.NextDouble() * 1000 + 1), 2),
        StockQuantity = random.Next(0, 1000),
        Sku = $"SKU-{i:D6}",
        IsActive = random.NextDouble() > 0.1,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(0, 365))
    });
}

db.Products.AddRange(products);
await db.SaveChangesAsync();
Console.WriteLine("Products created.");

// Generate 10000 orders with 1-10 order items each
Console.WriteLine("Creating 10000 orders with order items...");
var orders = new List<Order>(10000);
var orderItems = new List<OrderItem>();

for (int i = 1; i <= 10000; i++)
{
    var order = new Order
    {
        CustomerName = $"Customer {i}",
        CustomerEmail = $"customer{i}@example.com",
        ShippingAddress = $"{random.Next(1, 9999)} Main Street, City {random.Next(1, 100)}, Country",
        OrderDate = DateTimeOffset.UtcNow.AddDays(-random.Next(0, 180)),
        Status = statuses[random.Next(statuses.Length)],
        TotalAmount = 0
    };
    orders.Add(order);
}

db.Orders.AddRange(orders);
await db.SaveChangesAsync();
Console.WriteLine("Orders created.");

Console.WriteLine("Creating order items...");
foreach (var order in orders)
{
    var itemCount = random.Next(1, 11);
    decimal orderTotal = 0;

    for (int j = 0; j < itemCount; j++)
    {
        var product = products[random.Next(products.Count)];
        var quantity = random.Next(1, 6);
        var discount = Math.Round((decimal)(random.NextDouble() * 0.2), 2);
        var unitPrice = product.Price;

        orderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Discount = discount
        });

        orderTotal += unitPrice * quantity * (1 - discount);
    }

    order.TotalAmount = Math.Round(orderTotal, 2);
}

db.OrderItems.AddRange(orderItems);
await db.SaveChangesAsync();
Console.WriteLine("Order items created.");

Console.WriteLine($"Done! Created {products.Count} products, {orders.Count} orders, and {orderItems.Count} order items.");

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

// Query 1: Simple query getting product properties with a where clause
Console.WriteLine("\n--- Query 1: Active products with price > 500 ---");
stopwatch.Restart();
var maxPrice = 500;
var expensiveActiveProducts = await db.Products
    .Where(p => p.IsActive && p.Price > maxPrice)
    .Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity })
    .OrderByDescending(p => p.Price)
    .ToListAsync();
stopwatch.Stop();
Console.WriteLine($"  Execution time: {stopwatch.ElapsedMilliseconds} ms ({expensiveActiveProducts.Count} results)");

// Query 2: Orders which bought a specific product
Console.WriteLine("\n--- Query 2: Orders containing Product 1 ---");
stopwatch.Restart();
var productId = 1;
var ordersWithProduct1 = await db.Orders
    .Where(o => o.OrderItems.Any(oi => oi.ProductId == productId))
    .Select(o => new { o.Id, o.CustomerName, o.OrderDate, o.TotalAmount })
    .Skip(100)
    .Take(100)
    .ToListAsync();
stopwatch.Stop();
Console.WriteLine($"  Execution time: {stopwatch.ElapsedMilliseconds} ms ({ordersWithProduct1.Count} results)");
// Query 3: GroupBy to get total count of products ordered by customer
Console.WriteLine("\n--- Query 3: Total products ordered per customer ---");
stopwatch.Restart();
var productCountsByCustomer = await db.OrderItems
    .GroupBy(oi => oi.Order.CustomerName)
    .Select(g => new { CustomerName = g.Key, TotalProductsOrdered = g.Sum(oi => oi.Quantity) })
    .OrderByDescending(x => x.TotalProductsOrdered)
    .ToListAsync();
stopwatch.Stop();
Console.WriteLine($"  Execution time: {stopwatch.ElapsedMilliseconds} ms ({productCountsByCustomer.Count} results)");

Console.WriteLine("Finished all queries.");
Console.WriteLine("Now look into your SQL Server Profiler to see the generated SQL statements.");
