using Sandbox;
using System.Collections.Generic;

namespace SboxPro.Inventory;

/// <summary>
/// Asset for items that are consumed on use (potions, food, scrolls). Extension `.consumable`.
/// </summary>
[AssetType( Name = "Consumable", Extension = "consumable", Category = "Items" )]
public class ConsumableDefinition : ItemDefinition
{
	// ============ INSTANT EFFECTS ============

	/// <summary>HP restored immediately on use.</summary>
	[Property, Range( 0, 9999 )] public float HealthRestore { get; set; } = 0f;

	/// <summary>Hunger points restored immediately (0 if hunger system isn't used).</summary>
	[Property, Range( 0, 9999 )] public float HungerRestore { get; set; } = 0f;

	/// <summary>Stamina restored immediately.</summary>
	[Property, Range( 0, 9999 )] public float StaminaRestore { get; set; } = 0f;

	/// <summary>Mana restored immediately.</summary>
	[Property, Range( 0, 9999 )] public float ManaRestore { get; set; } = 0f;

	// ============ TIMED BUFFS ============

	/// <summary>Time-limited stat buffs applied on use (e.g. +20% speed for 30s).</summary>
	[Property] public List<TimedStatModifier> BuffsOnUse { get; set; } = new();

	// ============ TIMING ============

	/// <summary>Animation/usage duration in seconds. The player is busy for this long.</summary>
	[Property, Range( 0, 60 )] public float UseDuration { get; set; } = 1f;

	/// <summary>Cooldown before the same consumable can be used again.</summary>
	[Property, Range( 0, 600 )] public float CooldownSeconds { get; set; } = 0f;

	// ============ AUDIO ============

	/// <summary>Sound played on use (gulp, sip, scroll-tear, etc.).</summary>
	[Property] public SoundEvent UseSound { get; set; }
}
