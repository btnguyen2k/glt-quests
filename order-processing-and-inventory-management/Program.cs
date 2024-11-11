using System.ComponentModel.DataAnnotations;
using Microsoft.VisualBasic;

const int MaxItemsPerBucket = 10000;

const int NumItemsToGenerate = 100000;
const int NumOrdersToGenerate = 100000000;

const string dataDir = "./data100000";
const string filenameInventory = $"{dataDir}/inventory.csv";
const string filenameInventoryOut = $"{dataDir}/inventory_out.csv";
const string filenameOrders = $"{dataDir}/orders.csv";
const string filenameOrdersResult = $"{dataDir}/orders_result.csv";
const string filenameOrdersOut = $"{dataDir}/orders_out.json";

Random random = new();
var ATOZ = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
var itemCategories = new List<string> { };
for (int i = 0; i < ATOZ.Length; i++)
{
    for (int j = 0; j < ATOZ.Length; j++)
    {
        itemCategories.Add($"{ATOZ[i]}{ATOZ[j]}");
    }
}
itemCategories = [.. itemCategories.OrderBy(x => random.Next())];

var numCategories = Math.Max(2, NumItemsToGenerate / MaxItemsPerBucket);
var usedCategories = itemCategories[..numCategories];
var maxItemsPerOrder = Math.Max(10, Math.Min(100, NumItemsToGenerate / 10));

var inventory = GenerateRandomItems([.. usedCategories], NumItemsToGenerate, 65 * maxItemsPerOrder * (NumOrdersToGenerate / NumItemsToGenerate) / 100);

Console.WriteLine($"Writing items to file '{filenameInventory}'...");
using (var writer = new StreamWriter(filenameInventory))
{
    writer.WriteLine("sku,item_name,price,quantity");
    foreach (var item in inventory.GetItems())
    {
        writer.WriteLine($"{item.Sku},{item.Name},{item.Price * 100 / 100},{item.Quantity}");
    }
}

inventory = GenerateRandomOrders(inventory, NumOrdersToGenerate, maxItemsPerOrder, filenameOrders, filenameOrdersResult, filenameOrdersOut);

Console.WriteLine($"Writing inventory result to file '{filenameInventoryOut}'...");
using (var writer = new StreamWriter(filenameInventoryOut))
{
    writer.WriteLine("sku,quantity");
    foreach (var item in inventory.GetItems())
    {
        writer.WriteLine($"{item.Sku},{item.Quantity}");
    }
}

Console.WriteLine("DONE.");

Inventory GenerateRandomItems(string[] itemCategories, int numItems, int maxQuantity)
{
    decimal[] PriceParts = [0.19m, 0.29m, 0.39m, 0.49m, 0.59m, 0.69m, 0.79m, 0.89m, 0.99m];
    decimal[] PricePartsZero = [0.49m, 0.99m];
    Random random = new();

    Console.WriteLine($"Generating {numItems} random items, using categories: {string.Join(", ", itemCategories)}");

    var inventory = new Inventory();
    for (int i = 0; i < numItems; i++)
    {
        var bucketId = i % numCategories;
        var itemIdInBucket = i / numCategories;

        var item = new Item
        {
            Sku = $"{itemCategories[bucketId]}{itemIdInBucket.ToString("D4")}",
            Name = $"Item {i.ToString("X6")}",
            Price = random.Next(1, 100) + PriceParts[random.Next(PriceParts.Length)],
            Quantity = 10 + random.Next(maxQuantity)
        };
        if (item.Price < 1.00m)
        {
            item.Price = PricePartsZero[random.Next(PricePartsZero.Length)];
        }
        inventory.AddItem(item);
    }
    return inventory.SuffleItems();
}

Inventory GenerateRandomOrders(Inventory inventory, int numOrders, int maxItemsPerOrder, string filenameOrders = "orders.csv", string filenameOrdersResult = "orders_result.csv", string filenameOrdersOut = "orders_out.json")
{
    Console.WriteLine($"Generating {numOrders} random orders, with up to {maxItemsPerOrder} items each");
    Random random = new();

    using var writerOrders = new StreamWriter(filenameOrders);
    writerOrders.WriteLine("order_id,sku,quantity");

    using var writerOrdersOut = new StreamWriter(filenameOrdersOut);

    using var writerOrdersResult = new StreamWriter(filenameOrdersResult);
    writerOrdersResult.WriteLine("order_id,success,sku,quantity,total_cost");

    var numOrdersSuccessful = 0;
    var numOrdersFailed = 0;
    var totalRevenue = 0.00m;

    int numItems = inventory.GetItems().Count;

    for (int i = 0; i < numOrders; i++)
    {
        var order = new Order
        {
            OrderId = $"O{i.ToString("X6")}",
            Sku = inventory.GetItem(random.Next(numItems))?.Sku ?? $"O{random.Next(i).ToString("X6")}",
            Quantity = random.Next(1, maxItemsPerOrder + 1)
        };
        writerOrders.WriteLine($"{order.OrderId},{order.Sku},{order.Quantity}");

        order = inventory.ProcessOrder(order);
        if (order.IsSuccessful)
        {
            numOrdersSuccessful++;
            totalRevenue += order.TotalCost;
        }
        else
        {
            numOrdersFailed++;
        }

        writerOrdersResult.WriteLine($"{order.OrderId},{order.IsSuccessful},{order.Sku},{order.Quantity},{order.TotalCost}");
    }

    // write total_revenue, total_orders, success_orders, failed_orders to JSON file
    writerOrdersOut.WriteLine("{");
    writerOrdersOut.WriteLine($"  \"total_revenue\": {totalRevenue},");
    writerOrdersOut.WriteLine($"  \"total_orders\": {numOrders},");
    writerOrdersOut.WriteLine($"  \"success_orders\": {numOrdersSuccessful},");
    writerOrdersOut.WriteLine($"  \"failed_orders\": {numOrdersFailed}");
    writerOrdersOut.WriteLine("}");

    return inventory;
}

class Item
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; } = default;
    public int Quantity { get; set; } = default;
}

class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; } = default;
    public bool IsSuccessful { get; set; } = false;
    public decimal TotalCost { get; set; } = default;
}

class Inventory
{
    private List<Item> _itemsList = [];
    private IDictionary<string, Item> _itemsMap = new Dictionary<string, Item>();
    private Random _random = new();

    public void AddItem(Item item)
    {
        _itemsList.Add(item);
        _itemsMap.Add(item.Sku, item);
    }

    public Order ProcessOrder(Order order)
    {
        order.IsSuccessful = false;
        order.TotalCost = 0.00m;
        var item = _itemsMap[order.Sku];
        if (item != null)
        {
            if (item.Quantity >= order.Quantity)
            {
                item.Quantity -= order.Quantity;
                order.IsSuccessful = true;
                order.TotalCost = item.Price * order.Quantity;
            }
        }
        return order;
    }

    public Inventory SuffleItems()
    {
        _itemsList = [.. _itemsList.OrderBy(x => _random.Next())];
        return this;
    }

    public Item? GetItem(int index)
    {
        return index >= 0 && index < _itemsList.Count ? _itemsList[index] : null;
    }

    public List<Item> GetItems()
    {
        return _itemsList;
    }
}
