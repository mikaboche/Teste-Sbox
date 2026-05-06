using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Stat modifier applied while an item is equipped or active.
/// Combination semantics: Additive sums, Multiplicative multiplies, Override wins last.
/// </summary>
public struct ItemStatModifier
{
	/// <summary>Which stat to affect (Health, Speed, AttackPower, etc.).</summary>
	[Property] public StatType Type { get; set; }

	/// <summary>How much to add/multiply/override by (depends on Kind).</summary>
	[Property] public float Value { get; set; }

	/// <summary>Additive (+Value), Multiplicative (×Value), or Override (=Value, last wins).</summary>
	[Property] public StatModifierKind Kind { get; set; }
}

/// <summary>
/// Stat modifier with a duration (consumable buffs, environment effects).
/// Duration = 0 means permanent (until removed by source).
/// </summary>
public struct TimedStatModifier
{
	/// <summary>Which stat the buff affects.</summary>
	[Property] public StatType Type { get; set; }

	/// <summary>Magnitude of the modification (interpreted by Kind).</summary>
	[Property] public float Value { get; set; }

	/// <summary>Additive / Multiplicative / Override.</summary>
	[Property] public StatModifierKind Kind { get; set; }

	/// <summary>Buff lifetime in seconds. 0 = permanent until removed by source.</summary>
	[Property, Range( 0, 600 )] public float Duration { get; set; }
}

/// <summary>
/// One slot in a recipe's input list.
/// IsConsumed=false treats the ingredient as a tool (required to craft, not consumed).
/// </summary>
public struct RecipeIngredient
{
	/// <summary>The required item (Material, Component, even another tool).</summary>
	[Property] public ItemDefinition Item { get; set; }

	/// <summary>How many copies are needed.</summary>
	[Property, Range( 1, 99 )] public int Count { get; set; }

	/// <summary>If true the items are consumed on craft. If false they're checked-and-kept
	/// (use this for tools / catalysts / quest items required to craft but not consumed).</summary>
	[Property] public bool IsConsumed { get; set; }
}
