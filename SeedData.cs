using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>Fills an empty database with the default menu.</summary>
public static class SeedData
{
    private const string Food = "Food";
    private const string Beverages = "Beverages";
    private const string Vegan = "Vegan";
    private const string NonVegan = "Non-Vegan";
    private const string Vegetarian = "Vegetarian";

    /// <summary>Adds the sample menu once, to a brand-new database. Never again after that (even if the owner deletes every item).</summary>
    public static async Task EnsureSeededAsync(AppDbContext db, SettingsStore settings)
    {
        if (settings.Current.MenuSeeded) return;

        if (await db.MenuItems.AnyAsync())
        {
            settings.Update(s => s.MenuSeeded = true);
            return;
        }

        db.MenuItems.AddRange(
            // Food
            Item(1,  "Grilled Chicken Caesar Salad",    12.99m, Food, NonVegan,   "photo-1512621776951-a57141f2eefd", "Crisp romaine, grilled chicken breast, parmesan, and Caesar dressing."),
            Item(2,  "Classic Club Sandwich",           10.99m, Food, NonVegan,   "photo-1528735602780-2552fd46c7af", "Triple-decker with turkey, bacon, lettuce, tomato, and mayo."),
            Item(3,  "Spinach and Feta Stuffed Chicken", 14.99m, Food, NonVegan,  "photo-1604908176997-125f25cc6f3d", "Tender chicken breast filled with spinach and rich feta cheese."),
            Item(4,  "Vegetable Quinoa Bowl",           11.99m, Food, Vegan,      "photo-1546069901-ba9599a7e63c",    "Nutritious quinoa served with fresh seasoned roasted vegetables."),
            Item(5,  "BBQ Pulled Pork Sandwich",         9.99m, Food, NonVegan,   "photo-1627308595229-7830a5c91f9f", "Slow-cooked pulled pork topped with smoky BBQ sauce on a bun."),
            Item(6,  "Caprese Panini",                   8.99m, Food, NonVegan,   "photo-1509722747041-616f39b57569", "Fresh mozzarella, tomatoes, and basil pesto pressed on artisan bread."),
            Item(7,  "Fish Tacos",                      13.99m, Food, NonVegan,   "photo-1565299585323-38d6b0865b47", "Grilled fish served in warm tortillas with cabbage slaw and lime."),
            Item(8,  "Mushroom and Swiss Burger",       12.99m, Food, NonVegan,   "photo-1568901346375-23c9450c58cd", "Juicy beef patty topped with sautéed mushrooms and melted Swiss cheese."),
            Item(9,  "Quiche Lorraine",                 10.99m, Food, NonVegan,   "photo-1551024709-8f23befc6f87",    "Classic savory pastry crust filled with eggs, cream, bacon, and cheese."),
            Item(10, "Mediterranean Pasta",             13.99m, Food, Vegan,      "photo-1621996346565-e3d5d6281298", "Tossed pasta with olives, tomatoes, garlic, olive oil, and feta."),
            Item(11, "Asian Chicken Salad",             11.99m, Food, NonVegan,   "photo-1540420773420-3366772f4999", "Grilled chicken over mixed greens with sesame-ginger dressing and wonton strips."),
            Item(12, "Beef Stir-Fry",                   15.99m, Food, NonVegan,   "photo-1512058564366-18510be2db19", "Tender beef strips tossed with fresh vegetables and savory Asian sauce."),
            Item(13, "Margherita Pizza",                14.99m, Food, Vegetarian, "photo-1604382355076-af4b0eb60143", "Traditional crust topped with fresh mozzarella, tomatoes, and basil."),
            Item(14, "Roasted Vegetable Wrap",           9.99m, Food, Vegan,      "photo-1540713164800-2d51b3d68128", "Assorted seasoned roasted veggies wrapped in a soft tortilla."),
            Item(15, "Soup of the Day",                  5.99m, Food, Vegetarian, "photo-1547592166-23ac45744acd",    "Chef's freshly prepared seasonal soup served hot."),

            // Beverages
            Item(16, "Soft Drinks",                      2.99m, Beverages, Vegan, "photo-1622483767028-3f66f32aef97", "Choice of refreshing carbonated soft drinks."),
            Item(17, "Iced Tea",                         2.99m, Beverages, Vegan, "photo-1556679343-c7306c1976bc",    "Chilled freshly brewed black tea served over ice."),
            Item(18, "Freshly Squeezed Lemonade",        3.99m, Beverages, Vegan, "photo-1621263764928-df1444c5e859", "Made with fresh lemons and a touch of sweetness."),
            Item(19, "Fruit Smoothies",                  4.99m, Beverages, Vegan, "photo-1505252585461-04db1eb84625", "Blended seasonal fresh fruits with ice and juice."),
            Item(20, "Coffee",                           2.99m, Beverages, Vegan, "photo-1514432324607-a09d9b4aefdd", "Freshly brewed classic hot coffee."),
            Item(21, "Hot Tea",                          2.99m, Beverages, Vegan, "photo-1576092768241-dec231879fc3", "Selection of soothing hot brewed teas."),
            Item(22, "Bottled Water",                    1.99m, Beverages, Vegan, "photo-1548839140-29a749e1cf4d",    "Pure refreshing bottled spring water.")
        );

        await db.SaveChangesAsync();
        settings.Update(s => s.MenuSeeded = true);
    }

    private static MenuItem Item(int id, string name, decimal price, string category, string dietary, string photo, string description) =>
        new()
        {
            Id = id,
            Name = name,
            Price = price,
            Description = description,
            Category = category,
            Dietary = dietary,
            ImageUrl = $"https://images.unsplash.com/{photo}?auto=format&fit=crop&w=400&q=80"
        };
}
