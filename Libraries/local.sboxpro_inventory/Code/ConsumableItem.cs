using Sandbox;

namespace SboxPro.Inventory;

/// <summary>Runtime instance of a consumable. Tracks last-use time for cooldowns.</summary>
public sealed class ConsumableItem : GameResourceItem<ConsumableDefinition>
{
	public TimeSince TimeSinceLastUse;

	/// <summary>Consumables stack freely — same definition = same stack.</summary>
	public override bool CanStackWith( InventoryItem other )
		=> other is ConsumableItem c && c.Resource == Resource;
}
