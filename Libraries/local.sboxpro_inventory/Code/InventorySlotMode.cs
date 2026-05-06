// Vendored from conna.inventory (MIT, by conna).
// Namespace renamed Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// See LICENSE-NOTICES.md at sbox-pro repo root for full attribution.

using System;

namespace SboxPro.Inventory;

/// <summary>
/// Determines how items occupy space in an inventory.
/// </summary>
public enum InventorySlotMode
{
	/// <summary>
	/// Items occupy space based on their Width and Height properties (Tetris-style).
	/// </summary>
	Tetris,

	/// <summary>
	/// All items occupy exactly one slot regardless of their actual size.
	/// </summary>
	Single
}
