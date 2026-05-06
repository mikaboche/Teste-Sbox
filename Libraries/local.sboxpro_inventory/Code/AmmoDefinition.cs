using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Asset for ammunition. Stackable, fits the hotbar by default. Designers create via
/// Asset Browser → New → Ammo. Reuses <see cref="AmmoType"/> from WeaponDefinition so
/// a Pistol weapon and Pistol ammo share the same compatibility key.
/// </summary>
[AssetType( Name = "Ammo", Extension = "ammo", Category = "Items" )]
public class AmmoDefinition : ItemDefinition
{
	// ============ AMMO TYPE ============

	/// <summary>Ammo family (Pistol, Rifle, Shotgun, etc.). Weapons match against this.</summary>
	[Property] public AmmoType AmmoType { get; set; } = AmmoType.None;

	// ============ DAMAGE ============

	/// <summary>Multiplier applied to weapon damage when this ammo is loaded.
	/// 1.0 = no change, 1.5 = +50% damage, 0.5 = half damage.</summary>
	[Property, Range( 0, 10 )] public float DamageMultiplier { get; set; } = 1f;

	/// <summary>How many surfaces / armor layers the projectile can penetrate before stopping.</summary>
	[Property, Range( 0, 20 )] public int PenetrationPower { get; set; } = 0;

	// ============ PROJECTILE ============

	/// <summary>Projectile speed in world units per second. Hitscan-fast = ~10000+, slow arrow = ~2000.</summary>
	[Property, Range( 0, 50000 )] public float ProjectileSpeed { get; set; } = 10000f;

	/// <summary>Optional prefab spawned as the physical projectile. Leave null for hitscan weapons.</summary>
	[Property] public GameObject ProjectilePrefab { get; set; }

	// ============ COMPATIBILITY ============

	/// <summary>Whitelist of weapons that accept this ammo. When empty, any weapon with the
	/// matching <see cref="AmmoType"/> can use it.</summary>
	[Property] public List<WeaponDefinition> WeaponCompatibility { get; set; } = new();
}
