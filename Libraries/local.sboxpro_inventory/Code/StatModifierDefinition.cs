using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Asset for a named stat modifier — buff/debuff source. Extension `.stat_mod`.
/// Used for persistent or designer-authored buffs that come from items, environment
/// effects, or quest rewards. Different from inline ItemStatModifier (which is a
/// struct embedded in items): a StatModifierDefinition has its own Id, can be
/// stacked/refreshed by name, has its own VFX/icon, and can be referenced from
/// multiple sources.
/// </summary>
[AssetType( Name = "Stat Modifier", Extension = "stat_mod", Category = "Stats" )]
public class StatModifierDefinition : GameResource
{
	// ============ IDENTIFICATION ============

	/// <summary>Stable, designer-assigned identifier (e.g. "buff_strength_minor"). Used for stacking/refresh logic.</summary>
	[Property] public string Id { get; set; } = "";

	/// <summary>Display name shown in the buff bar tooltip.</summary>
	[Property] public string DisplayName { get; set; } = "Modifier";

	/// <summary>Free-form description shown in the buff tooltip.</summary>
	[Property, TextArea] public string Description { get; set; } = "";

	/// <summary>Icon shown in the buff bar.</summary>
	[Property, ImageAssetPath] public string IconPath { get; set; }

	// ============ EFFECT ============

	/// <summary>Which stat this modifier touches.</summary>
	[Property] public StatType Type { get; set; } = StatType.AttackPower;

	/// <summary>Magnitude of the modification (interpreted by Kind).</summary>
	[Property] public float Value { get; set; } = 0f;

	/// <summary>Additive (+Value), Multiplicative (×Value), or Override (=Value, last wins).</summary>
	[Property] public StatModifierKind Kind { get; set; } = StatModifierKind.Additive;

	// ============ DURATION ============

	/// <summary>How long the modifier lasts in seconds. 0 = permanent until removed by source.</summary>
	[Property, Range( 0, 600 )] public float Duration { get; set; } = 0f;

	// ============ STACKING ============

	/// <summary>Behaviour when the player already has this modifier active:
	/// RefreshDuration = restart timer; StackUpToCap = add another instance up to MaxStacks;
	/// DoNotStack = ignore.</summary>
	[Property] public ModifierStackKind StackKind { get; set; } = ModifierStackKind.RefreshDuration;

	/// <summary>Cap for StackUpToCap behaviour.</summary>
	[Property, ShowIf( nameof( StackKind ), ModifierStackKind.StackUpToCap ), Range( 1, 99 )]
	public int MaxStacks { get; set; } = 1;

	// ============ CATEGORY (for UI grouping) ============

	/// <summary>Category that drives the buff icon's tint and grouping in the buff bar.</summary>
	[Property] public ModifierCategory Category { get; set; } = ModifierCategory.Buff;
}
