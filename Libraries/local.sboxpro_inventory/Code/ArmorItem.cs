using Sandbox;

namespace SboxPro.Inventory;

/// <summary>Runtime instance of an armor piece. Holds per-piece durability state.</summary>
public sealed class ArmorItem : GameResourceItem<ArmorDefinition>
{
	public int CurrentDurability { get; set; }

	/// <summary>Armor never stacks — each piece is unique by its durability state.</summary>
	public override bool CanStackWith( InventoryItem other ) => false;

	protected override void OnResourceUpdated( ArmorDefinition def )
	{
		if ( def is null ) return;
		CurrentDurability = def.HasDurability ? def.MaxDurability : 0;
	}

	public override InventoryItem CreateStackClone( int stackCount )
	{
		var clone = (ArmorItem)base.CreateStackClone( stackCount );
		clone.LoadFromResource( Resource );
		clone.CurrentDurability = CurrentDurability;
		return clone;
	}
}
