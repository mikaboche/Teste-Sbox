using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Runtime instance of a Blueprint. Pure stackable — same blueprint can stack until
/// the player learns the recipe; afterwards extra copies become "already known" no-ops
/// (or get consumed anyway, depending on BlueprintDefinition.ConsumeIfAlreadyKnown).
/// </summary>
public sealed class BlueprintItem : GameResourceItem<BlueprintDefinition>
{
	public override bool CanStackWith( InventoryItem other )
		=> other is BlueprintItem b && b.Resource == Resource;
}
