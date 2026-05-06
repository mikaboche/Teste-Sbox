using Sandbox;

namespace SboxPro.Inventory;

/// <summary>Runtime instance of a crafting material. Pure stackable resource.</summary>
public sealed class MaterialItem : GameResourceItem<MaterialDefinition>
{
	public override bool CanStackWith( InventoryItem other )
		=> other is MaterialItem m && m.Resource == Resource;
}
