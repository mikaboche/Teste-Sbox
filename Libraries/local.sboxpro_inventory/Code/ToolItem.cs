using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Runtime instance of a tool. Tracks current durability — same pattern as WeaponItem.
/// Stacking is restricted to fresh, full-durability copies.
/// </summary>
public sealed class ToolItem : GameResourceItem<ToolDefinition>
{
	public int CurrentDurability { get; set; }
	public TimeSince TimeSinceSwing;

	public override bool CanStackWith( InventoryItem other )
	{
		if ( other is not ToolItem t ) return false;
		if ( t.Resource != Resource ) return false;
		return CurrentDurability == t.CurrentDurability;
	}

	protected override void OnResourceUpdated( ToolDefinition def )
	{
		if ( def is null ) return;
		CurrentDurability = def.HasDurability ? def.MaxDurability : 0;
	}

	public override InventoryItem CreateStackClone( int stackCount )
	{
		var clone = (ToolItem)base.CreateStackClone( stackCount );
		clone.LoadFromResource( Resource );
		clone.CurrentDurability = CurrentDurability;
		return clone;
	}
}
