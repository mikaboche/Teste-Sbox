namespace SboxPro.Inventory;

/// <summary>Rarity tier for items. Drives tooltip border colour, name colour, and loot popup category.</summary>
public enum Rarity
{
	Common,
	Uncommon,
	Rare,
	Epic,
	Legendary
}

/// <summary>
/// Where an equippable item slots into the player's equipment storage.
/// </summary>
public enum EquipmentSlotType
{
	None,
	Head,
	Face,
	Chest,
	Back,
	Shoulders,
	Hands,
	Legs,
	Feet,
	Necklace,
	Ring1, Ring2,
	Earring1, Earring2,
	MainHand, OffHand,
	Tool1, Tool2,
	Custom1, Custom2, Custom3
}

public enum WeaponSlot { Primary, Secondary, Melee, Throwable }

public enum WeaponHandedness { OneHanded, TwoHanded, OffHand }

public enum AmmoType { None, Pistol, Rifle, Shotgun, Bow, Custom1, Custom2 }

public enum MaterialTier { Wood, Stone, CopperBronze, IronSteel, Mithril, Adamant, Custom1, Custom2 }

public enum StatType
{
	Health, HealthMax,
	Mana, ManaMax,
	Stamina, StaminaMax,
	Hunger, HungerMax,
	AttackPower,
	Defense,
	Speed,         // movement multiplier
	AttackSpeed,   // weapon fire-rate multiplier
	CritChance,
	CritDamage,
	Custom1, Custom2, Custom3
}

public enum StatModifierKind
{
	Additive,         // +10
	Multiplicative,   // x1.5
	Override          // = 50 (last Override wins, ignores Add/Mul)
}

public enum ModifierStackKind
{
	RefreshDuration,   // re-apply resets duration
	StackUpToCap,      // stacks accumulate
	DoNotStack         // ignore if already present
}

public enum ModifierCategory
{
	Buff,
	Debuff,
	Equipment,
	Consumable,
	EnvironmentEffect
}

/// <summary>What kind of gathering tool this is. Drives the gather logic switch.</summary>
public enum ToolType
{
	None,
	Pickaxe,
	Axe,
	FishingRod,
	Hammer,
	Sickle,
	Shovel,
	Custom1, Custom2
}

/// <summary>Crafting station type. Recipes filter by this + Tier.</summary>
public enum CraftingStationType
{
	None,
	Workbench,
	Forge,
	Alchemy,
	Cooking,
	Tailoring,
	Carpentry,
	Enchanting,
	Custom1, Custom2
}
