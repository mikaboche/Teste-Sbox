// Vendored from conna.inventory (MIT, by conna).
// Namespace renamed Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// See LICENSE-NOTICES.md at sbox-pro repo root for full attribution.

namespace SboxPro.Inventory;

/// <summary>
/// Defines how an inventory broadcasts network updates to clients.
/// </summary>
public enum NetworkMode
{
	/// <summary>
	/// Only clients that have been explicitly subscribed via <see cref="BaseInventory.AddSubscriber"/>
	/// will receive inventory updates. Best for player-specific inventories where not everyone
	/// needs to see the contents.
	/// </summary>
	Subscribers,

	/// <summary>
	/// All connected clients automatically receive inventory updates.
	/// Best for shared world inventories that everyone can see.
	/// </summary>
	Global
}
