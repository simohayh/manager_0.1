/// <summary>Values stored in <see cref="Order.Status"/>.</summary>
public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Cooking = "Cooking";
    public const string Ready = "Ready";
    public const string Delivered = "Delivered";
    public const string Completed = "Completed";
    public const string OutOfStock = "Out of Stock";
}

/// <summary>Values stored in <see cref="Order.PaymentStatus"/>.</summary>
public static class PaymentState
{
    public const string Unpaid = "Unpaid";
    public const string AwaitingVerification = "AwaitingVerification";
    public const string Paid = "Paid";
}

/// <summary>Input limits enforced by the server (never trust the browser to enforce them).</summary>
public static class Limits
{
    public const int MaxTableNumber = 50;
    public const int MaxItemsPerOrder = 50;
    public const int MaxQuantity = 100;
    public const int MaxNotesLength = 500;
    public const int MaxCodesPerRequest = 50;
    public const int MaxPrepMinutes = 180;
}

<<<<<<< HEAD
/// <summary>The staff roles. Each has its own PIN and its own login cookie. "owner" runs the admin page (menu, reports, settings).</summary>
=======
/// <summary>The two staff roles. Each has its own PIN and its own login cookie.</summary>
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
public static class StaffRoles
{
    public const string Kitchen = "kitchen";
    public const string Cashier = "cashier";
<<<<<<< HEAD
    public const string Owner = "owner";

    public static bool IsValid(string? role) => role == Kitchen || role == Cashier || role == Owner;
=======

    public static bool IsValid(string? role) => role == Kitchen || role == Cashier;
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
}
