using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Asset for gathering tools (pickaxe, axe, fishing rod, etc.). Asset extension `.tool`.
/// Tools share the MainHand visual slot with weapons — the AttachmentPrefab + offset
/// follow the same convention. Gather logic reads <see cref="GatherTier"/> against
/// the resource node's required tier.
/// </summary>
[AssetType( Name = "Tool", Extension = "tool", Category = "Items" )]
public class ToolDefinition : ItemDefinition
{
	// ============ TOOL CLASS ============

	/// <summary>Which gathering action this tool performs (Pickaxe → mining nodes,
	/// Axe → trees, FishingRod → water, etc.).</summary>
	[Property] public ToolType ToolType { get; set; } = ToolType.Pickaxe;

	// ============ GATHER ============

	/// <summary>Highest resource tier the tool can harvest. A Tier-3 pickaxe can mine
	/// Tier-1, Tier-2, and Tier-3 nodes; Tier-4 nodes refuse it.</summary>
	[Property, Range( 1, 20 )] public int GatherTier { get; set; } = 1;

	/// <summary>Multiplier on the swing/cast time. 1.0 = base, 2.0 = twice as fast.</summary>
	[Property, Range( 0.05f, 10 )] public float GatherSpeed { get; set; } = 1f;

	/// <summary>Cooldown between swings/casts in seconds.</summary>
	[Property, Range( 0.05f, 10 )] public float SwingCooldown { get; set; } = 0.5f;

	/// <summary>Optional whitelist — when non-empty, this tool can ONLY harvest these resources.
	/// Empty list means "any resource the gather tier permits".</summary>
	[Property] public List<MaterialDefinition> TargetResources { get; set; } = new();

	// ============ DURABILITY ============

	/// <summary>If true, the tool wears out with use.</summary>
	[Property] public bool HasDurability { get; set; } = true;

	/// <summary>Swings before the tool breaks. Visible only when HasDurability=true.</summary>
	[Property, ShowIf( nameof( HasDurability ), true ), Range( 1, 10000 )]
	public int MaxDurability { get; set; } = 250;

	// ============ ATTACHMENT (visual on player bone, same convention as WeaponDefinition) ============

	/// <summary>Prefab attached to the hand bone when the tool is in MainHand.</summary>
	[Property] public GameObject AttachmentPrefab { get; set; }

	/// <summary>Local position offset of the attachment relative to the bone.</summary>
	[Property] public Vector3 AttachmentOffset { get; set; }

	/// <summary>Local rotation of the attachment relative to the bone.</summary>
	[Property] public Angles AttachmentRotation { get; set; }

	// ============ STATS WHILE EQUIPPED ============

	/// <summary>Stat modifiers applied while the tool is held (e.g. +1 GatherTier, +0.1 Speed).</summary>
	[Property] public List<ItemStatModifier> StatsWhileEquipped { get; set; } = new();
}
