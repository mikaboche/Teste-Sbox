using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Pure DATA asset for a crafting recipe — inputs, output, station requirement.
/// Extension `.recipe`. Inherits directly from <see cref="GameResource"/> (NOT
/// ItemDefinition — recipes are knowledge / rules, not items).
///
/// <para>How recipes get into a player's known list:
/// <list type="bullet">
/// <item>If <see cref="DiscoveredByDefault"/> is true, every player starts with this recipe known.</item>
/// <item>Otherwise the recipe is locked until a <see cref="BlueprintDefinition"/> that teaches
/// it is used by the player.</item>
/// </list></para>
/// </summary>
[AssetType( Name = "Crafting Recipe", Extension = "recipe", Category = "Crafting" )]
public class CraftingRecipeDefinition : GameResource
{
	// ============ IDENTIFICATION ============

	/// <summary>Stable, designer-assigned identifier (e.g. "iron_sword_recipe"). Used by saves and Blueprint refs.</summary>
	[Property] public string Id { get; set; } = "";

	/// <summary>Display name shown in the crafting UI.</summary>
	[Property] public string DisplayName { get; set; } = "Recipe";

	/// <summary>Free-form description / flavour text shown in the recipe tooltip.</summary>
	[Property, TextArea] public string Description { get; set; } = "";

	/// <summary>Path to the icon shown next to the recipe entry.</summary>
	[Property, ImageAssetPath] public string IconPath { get; set; }

	// ============ INPUTS ============

	/// <summary>List of (item, count) pairs the recipe consumes.</summary>
	[Property] public List<RecipeIngredient> Ingredients { get; set; } = new();

	// ============ OUTPUT ============

	/// <summary>The item produced by the recipe. Drag any ItemDefinition asset.</summary>
	[Property] public ItemDefinition ResultItem { get; set; }

	/// <summary>How many copies of ResultItem one craft produces.</summary>
	[Property, Range( 1, 99 )] public int ResultCount { get; set; } = 1;

	/// <summary>If true and the result has durability, it spawns at MaxDurability instead of degraded.</summary>
	[Property] public bool ResultStartsAtMaxDurability { get; set; } = true;

	// ============ STATION REQUIREMENT ============

	/// <summary>Which crafting station type the player must stand near to craft this recipe.</summary>
	[Property] public CraftingStationType RequiredStationType { get; set; } = CraftingStationType.None;

	/// <summary>Minimum tier the station must have. 1 = lowest.</summary>
	[Property, Range( 1, 10 )] public int RequiredStationTier { get; set; } = 1;

	/// <summary>Additional tag requirements on the station (e.g. ["wet_workbench"]).</summary>
	[Property] public List<string> RequiredStationTags { get; set; } = new();

	// ============ TIMING ============

	/// <summary>Crafting time in seconds. 0 = instant.</summary>
	[Property, Range( 0, 60 )] public float CraftingTime { get; set; } = 0f;

	// ============ SKILL / LEVEL GATING ============

	/// <summary>If true, the recipe needs a specific skill at a level to craft (in addition to being known).</summary>
	[Property] public bool HasSkillRequirement { get; set; } = false;

	/// <summary>Skill identifier (e.g. "blacksmithing", "alchemy"). Visible only when HasSkillRequirement=true.</summary>
	[Property, ShowIf( nameof( HasSkillRequirement ), true )]
	public string RequiredSkill { get; set; } = "";

	/// <summary>Minimum level in RequiredSkill. Visible only when HasSkillRequirement=true.</summary>
	[Property, ShowIf( nameof( HasSkillRequirement ), true ), Range( 1, 100 )]
	public int RequiredLevel { get; set; } = 1;

	// ============ DISCOVERY ============

	/// <summary>If true, players know this recipe from the start. False = must be unlocked via a Blueprint.</summary>
	[Property] public bool DiscoveredByDefault { get; set; } = true;
}
