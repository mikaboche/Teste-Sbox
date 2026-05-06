// Vendored from conna.inventory (MIT, by conna). Namespace renamed.

namespace SboxPro.Inventory;

/// <summary>
/// Where an item lives inside an inventory.
/// </summary>
public readonly record struct InventorySlot( int X, int Y, int W, int H );
