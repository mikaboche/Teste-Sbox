using Sandbox;

namespace SboxPro.Inventory;

/// <summary>Runtime instance of an ammo round. Pure stackable — no per-instance state.</summary>
public sealed class AmmoItem : GameResourceItem<AmmoDefinition>
{
	public override bool CanStackWith( InventoryItem other )
		=> other is AmmoItem a && a.Resource == Resource;
}
