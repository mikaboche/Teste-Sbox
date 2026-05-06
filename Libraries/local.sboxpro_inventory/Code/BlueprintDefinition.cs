using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Asset for a Blueprint — a consumable ITEM that, when used, teaches the holder a
/// CraftingRecipeDefinition. Asset extension `.tome` (the word `blueprint` and a
/// number of related forms like `recipe_book` are reserved by the Source 2 asset
/// compiler for internal placement/entity types and yield "Invalid Dependency
/// Information" errors when reused).
///
/// Workflow: blueprint drops as loot or sits in a vendor. Player picks it up and
/// "uses" it from the hotbar; the gameplay layer reads TaughtRecipe, adds the recipe
/// Id to the player's KnownRecipes set, and destroys the blueprint (one-shot use).
/// If the player already knows the recipe, the use should fail with a "you already
/// know this" message and the item stays in the inventory (or is consumed anyway,
/// designer's call via ConsumeIfAlreadyKnown).
/// </summary>
[AssetType( Name = "Blueprint", Extension = "tome", Category = "Items" )]
public class BlueprintDefinition : ItemDefinition
{
	// ============ TAUGHT RECIPE ============

	/// <summary>The crafting recipe this blueprint adds to the player's known list when used.</summary>
	[Property] public CraftingRecipeDefinition TaughtRecipe { get; set; }

	// ============ USAGE ============

	/// <summary>Animation/usage duration in seconds. The player is busy for this long.</summary>
	[Property, Range( 0, 30 )] public float UseDuration { get; set; } = 1.5f;

	/// <summary>Sound played when the blueprint is consumed.</summary>
	[Property] public SoundEvent UseSound { get; set; }

	/// <summary>If true and the player already knows TaughtRecipe, the blueprint is still
	/// consumed (designer choice — some games keep it as decor / collectible duplicate).
	/// If false, the use fails and the item stays.</summary>
	[Property] public bool ConsumeIfAlreadyKnown { get; set; } = false;
}
