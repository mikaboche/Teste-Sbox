using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Abstract base for any item asset. Designers never create `.item` directly — they
/// create one of the concrete subclasses (`.weapon`, `.armor`, `.consumable`,
/// `.material_item`, `.ammo`, `.tool`, `.deployable`) via the Asset Browser → New →
/// [category] menu.
///
/// <para><c>BaseItemAsset</c> (vendored from conna) already provides:
/// DisplayName, Category, MaxStackSize, Width, Height — all <c>[Property]</c>'d.</para>
///
/// We add the cross-category fields below. Every property is doc-commented so the
/// Inspector shows tooltips on hover (XML doc generation is on in csproj).
/// </summary>
public abstract class ItemDefinition : BaseItemAsset
{
	// ============ IDENTIFICATION ============

	/// <summary>Stable, designer-assigned identifier (e.g. "iron_sword", "potion_health_minor").
	/// Used by save files, recipes, and gameplay lookups — keep stable across renames.</summary>
	[Property] public string Id { get; set; } = "";

	/// <summary>Free-form description shown in tooltips and UI. Multi-line allowed.</summary>
	[Property, TextArea] public string Description { get; set; } = "";

	// ============ VISUAL ============

	/// <summary>Path to the 2D icon shown in inventory slots. PNG/JPG under the project root.</summary>
	[Property, ImageAssetPath] public string IconPath { get; set; }

	/// <summary>Mesh shown in the world when the item is dropped. Defaults to a simple box if unset.</summary>
	[Property] public Model WorldModel { get; set; }

	/// <summary>Mesh used in the 3D preview/tooltip view. Often higher-detail than WorldModel.</summary>
	[Property] public Model PreviewModel { get; set; }

	/// <summary>Optional VFX prefab spawned as a child of the dropped world item. Use for
	/// rarity glows, magical sparkles, smoke trails, etc. Build the prefab with a
	/// ParticleEffect Component + emitter/renderer children, then drag the .prefab here.
	/// Leave null for no effect.</summary>
	[Property] public GameObject WorldDropVfx { get; set; }

	/// <summary>Rarity tier — drives tooltip border colour, name colour, and loot popup category.</summary>
	[Property] public Rarity Rarity { get; set; } = Rarity.Common;

	// ============ ECONOMY & GATING ============

	/// <summary>Base sell value in the default currency. Vendors / loot tables can scale this.</summary>
	[Property, Range( 0, 999999 )] public int BaseValue { get; set; } = 0;

	/// <summary>Minimum player level required to use/equip this item. 1 = no gating.</summary>
	[Property, Range( 1, 100 )] public int LevelRequirement { get; set; } = 1;

	// ============ WEIGHT ============

	/// <summary>Encumbrance contribution. Items "without weight" should set this to 0.</summary>
	[Property, Range( 0, 1000 )] public float Weight { get; set; } = 1f;

	// ============ HOTBAR ELIGIBILITY ============

	/// <summary>If false, the hotbar refuses to accept this item even if a slot is free.
	/// Set false on raw materials and quest items so they don't clutter the quick-bar.</summary>
	[Property] public bool AllowInHotbar { get; set; } = true;

	// ============ USAGE FLAGS ============

	/// <summary>Whether the player can drop this item to the world (DropToWorld).</summary>
	[Property] public bool CanBeDropped { get; set; } = true;

	/// <summary>Whether vendors will buy this item.</summary>
	[Property] public bool CanBeSold { get; set; } = true;

	/// <summary>If true, this item can't be dropped, sold, or destroyed — keeps quest progress safe.</summary>
	[Property] public bool IsQuestItem { get; set; } = false;

	// ============ TAGS ============

	/// <summary>Free-form tags for filtering (e.g. "fire", "two-handed", "magical"). Used by
	/// loot tables, equipment sets, recipe filters.</summary>
	[Property] public List<string> Tags { get; set; } = new();

	// ============ ALIASES (renaming-safe persistence) ============

	/// <summary>Old Ids this item has been renamed from. Save files written with an old Id
	/// resolve to this asset if the current Id is in the alias list — prevents breakage
	/// when designers rename items.</summary>
	[Property] public List<string> Aliases { get; set; } = new();

	// ============ HELPERS ============

	/// <summary>True when MaxStackSize &gt; 1 (multiple copies fit one slot).</summary>
	public bool Stackable => MaxStackSize > 1;

	/// <summary>True when MaxStackSize == 1 (every copy needs its own slot).</summary>
	public bool Unique => MaxStackSize == 1;
}
