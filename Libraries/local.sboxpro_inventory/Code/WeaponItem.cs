using Sandbox;
using System;

namespace SboxPro.Inventory;

/// <summary>
/// Runtime instance of a weapon. State (ammo, durability) lives in fields here, not
/// on the immutable <see cref="WeaponDefinition"/> asset.
///
/// Note: <c>[Networked]</c> from conna's vendored attribute is intentionally NOT used
/// here — the upstream code-generator hook still mismatches the engine's signature
/// (sbox-pro Issue #10, upstream-tracked). State is replicated via the
/// <see cref="BaseInventory"/>'s subscriber pipeline whenever the inventory itself
/// re-broadcasts. For per-tick fields like ammo we'll use Component-side state in
/// later phases (e.g. WeaponHandler component reads these fields locally).
/// </summary>
public sealed class WeaponItem : GameResourceItem<WeaponDefinition>
{
	public int CurrentAmmo { get; set; }
	public int CurrentDurability { get; set; }

	public TimeSince TimeSinceShot;
	public TimeSince TimeSinceReload;

	/// <summary>
	/// Two weapons stack only when their per-instance state matches exactly.
	/// In practice that's "fresh, full-ammo, full-durability" copies of the same kind.
	/// </summary>
	public override bool CanStackWith( InventoryItem other )
	{
		if ( other is not WeaponItem w ) return false;
		if ( w.Resource != Resource ) return false;
		return CurrentAmmo == w.CurrentAmmo && CurrentDurability == w.CurrentDurability;
	}

	protected override void OnResourceUpdated( WeaponDefinition def )
	{
		if ( def is null ) return;
		CurrentAmmo = def.MaxAmmo;
		CurrentDurability = def.HasDurability ? def.MaxDurability : 0;
	}

	public override InventoryItem CreateStackClone( int stackCount )
	{
		var clone = (WeaponItem)base.CreateStackClone( stackCount );
		clone.LoadFromResource( Resource );
		clone.CurrentAmmo = CurrentAmmo;
		clone.CurrentDurability = CurrentDurability;
		return clone;
	}
}
