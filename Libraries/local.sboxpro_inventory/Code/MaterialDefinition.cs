using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Asset for crafting materials and resources. Extension `.material_item`.
/// </summary>
[AssetType( Name = "Crafting Material", Extension = "material_item", Category = "Items" )]
public class MaterialDefinition : ItemDefinition
{
	/// <summary>Material tier — drives recipe gating and tool/station requirements.</summary>
	[Property] public MaterialTier Tier { get; set; } = MaterialTier.Wood;

	/// <summary>Free-form tags consumed by crafting/upgrading stations to filter what
	/// they accept (e.g. ["smelter_input", "wood_log"]).</summary>
	[Property] public List<string> CraftingTags { get; set; } = new();
}
