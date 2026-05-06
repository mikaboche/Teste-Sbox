using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Concrete asset for any wearable armor piece. Asset extension `.armor`.
/// </summary>
[AssetType( Name = "Armor", Extension = "armor", Category = "Items" )]
public class ArmorDefinition : ItemDefinition
{
	// ============ DEFENSE ============

	/// <summary>Flat damage reduction this piece provides while equipped.</summary>
	[Property, Range( 0, 9999 )] public float Defense { get; set; } = 5f;

	/// <summary>Equipment slot the armor occupies (Head, Chest, Legs, Feet, etc.).</summary>
	[Property] public EquipmentSlotType Slot { get; set; } = EquipmentSlotType.Chest;

	// ============ VISUAL ============

	/// <summary>s&amp;box-native Clothing asset that rewrites the player mesh when equipped.
	/// Drag a .clothing asset here.</summary>
	[Property] public Clothing ClothingAsset { get; set; }

	// ============ DURABILITY ============

	/// <summary>If true, the armor has wear and can break.</summary>
	[Property] public bool HasDurability { get; set; } = true;

	/// <summary>Durability hits before the armor breaks. Visible only when HasDurability=true.</summary>
	[Property, ShowIf( nameof( HasDurability ), true ), Range( 1, 10000 )]
	public int MaxDurability { get; set; } = 100;

	// ============ STATS WHILE EQUIPPED ============

	/// <summary>Stat modifiers applied while equipped (e.g. +5 HealthMax, +2 Defense).</summary>
	[Property] public List<ItemStatModifier> StatsWhileEquipped { get; set; } = new();

	// ============ ATTACHMENT (capes, backpacks) ============

	/// <summary>Optional prefab attached to a bone when equipped (capes, backpacks, helmet plumes).</summary>
	[Property] public GameObject AttachmentPrefab { get; set; }

	/// <summary>Local position offset of the attachment relative to the bone.</summary>
	[Property] public Vector3 AttachmentOffset { get; set; }

	/// <summary>Local rotation of the attachment relative to the bone.</summary>
	[Property] public Angles AttachmentRotation { get; set; }
}
