using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Concrete asset for any combat-capable item. Asset extension `.weapon`.
/// Designers create via Asset Browser → New → Weapon.
/// </summary>
[AssetType( Name = "Weapon", Extension = "weapon", Category = "Items" )]
public class WeaponDefinition : ItemDefinition
{
	// ============ COMBAT ============

	/// <summary>Damage per hit/shot before any modifiers (ammo, buffs, crits).</summary>
	[Property, Range( 0, 9999 )] public float Damage { get; set; } = 10f;

	/// <summary>Shots/swings per second. 1 = once per second, 0.5 = every two seconds.</summary>
	[Property, Range( 0.01f, 30 )] public float FireRate { get; set; } = 1f;

	/// <summary>Maximum effective range in world units. Beyond this, hits are ignored or fall off.</summary>
	[Property, Range( 0, 100000 )] public float Range { get; set; } = 5000f;

	// ============ HANDLING ============

	/// <summary>One-handed (off-hand free), Two-handed (locks both hands), or OffHand-only.</summary>
	[Property] public WeaponHandedness Handedness { get; set; } = WeaponHandedness.OneHanded;

	/// <summary>Which equipment slot category this weapon occupies (Primary/Secondary/Melee/Throwable).</summary>
	[Property] public WeaponSlot Slot { get; set; } = WeaponSlot.Primary;

	// ============ AMMO ============

	/// <summary>What ammo type this weapon consumes. Match against AmmoDefinition.AmmoType.
	/// Use None for melee weapons.</summary>
	[Property] public AmmoType AmmoType { get; set; } = AmmoType.None;

	/// <summary>Magazine capacity. 0 for unlimited or melee.</summary>
	[Property, Range( 0, 9999 )] public int MaxAmmo { get; set; } = 12;

	/// <summary>Time in seconds to reload a full magazine.</summary>
	[Property, Range( 0, 60 )] public float ReloadDuration { get; set; } = 1.5f;

	// ============ DURABILITY ============

	/// <summary>If true, weapon has wear and can break. ToggleGroup with MaxDurability.</summary>
	[Property] public bool HasDurability { get; set; } = true;

	/// <summary>Durability hits before the weapon breaks. Visible only when HasDurability=true.</summary>
	[Property, ShowIf( nameof( HasDurability ), true ), Range( 1, 10000 )]
	public int MaxDurability { get; set; } = 100;

	// ============ STATS WHILE EQUIPPED ============

	/// <summary>Stat modifiers applied while this weapon is in an equipped slot
	/// (e.g. +20 AttackPower, ×0.9 Speed for heavy weapons).</summary>
	[Property] public List<ItemStatModifier> StatsWhileEquipped { get; set; } = new();

	// ============ AUDIO ============

	/// <summary>Sound played each time the weapon fires/swings.</summary>
	[Property] public SoundEvent FireSound { get; set; }

	/// <summary>Sound played at the start of a reload.</summary>
	[Property] public SoundEvent ReloadSound { get; set; }

	// ============ ATTACHMENT (visual on player bone) ============

	/// <summary>Prefab spawned and parented to a hand bone when equipped (the visible weapon model).</summary>
	[Property] public GameObject AttachmentPrefab { get; set; }

	/// <summary>Local position offset of the attachment relative to the bone.</summary>
	[Property] public Vector3 AttachmentOffset { get; set; }

	/// <summary>Local rotation of the attachment relative to the bone.</summary>
	[Property] public Angles AttachmentRotation { get; set; }
}
