using Sandbox;
using System;

namespace SboxPro.Inventory;

/// <summary>
/// Type-safe constructor for <see cref="InventoryItem"/> instances from their
/// associated <see cref="ItemDefinition"/> assets. Switch over the concrete
/// Definition subclass picks the matching runtime <see cref="GameResourceItem{T}"/>.
///
/// No generic fallback — adding a new item category requires extending this switch
/// alongside its Definition + Item subclass. That's the cost of type-safety; in
/// return, every code path knows what it's holding.
/// </summary>
public static class ItemFactory
{
	/// <summary>
	/// Instantiate a runtime item from an asset definition.
	/// Returns null if the definition type isn't mapped (caller should warn).
	/// </summary>
	public static InventoryItem CreateFromAsset( ItemDefinition def, int stackCount = 1 )
	{
		if ( def is null ) return null;

		// Typed dispatch — `dynamic` triggers System.Linq.Expressions/CallSite which
		// the s&box whitelist blocks (SB1000). Each branch instantiates the matching
		// runtime item and calls its strongly-typed LoadFromResource.
		InventoryItem item = def switch
		{
			WeaponDefinition w     => Make( w ),
			ArmorDefinition a      => Make( a ),
			ConsumableDefinition c => Make( c ),
			MaterialDefinition m   => Make( m ),
			AmmoDefinition am      => Make( am ),
			ToolDefinition t       => Make( t ),
			BlueprintDefinition b  => Make( b ),
			_ => null
		};

		if ( item is null )
		{
			Log.Warning( $"ItemFactory: no runtime mapping for definition type '{def.GetType().Name}'." );
			return null;
		}

		item.StackCount = Math.Clamp( stackCount, 1, def.MaxStackSize );
		return item;
	}

	private static WeaponItem Make( WeaponDefinition d )
	{
		var i = new WeaponItem();
		i.LoadFromResource( d );
		return i;
	}

	private static ArmorItem Make( ArmorDefinition d )
	{
		var i = new ArmorItem();
		i.LoadFromResource( d );
		return i;
	}

	private static ConsumableItem Make( ConsumableDefinition d )
	{
		var i = new ConsumableItem();
		i.LoadFromResource( d );
		return i;
	}

	private static MaterialItem Make( MaterialDefinition d )
	{
		var i = new MaterialItem();
		i.LoadFromResource( d );
		return i;
	}

	private static AmmoItem Make( AmmoDefinition d )
	{
		var i = new AmmoItem();
		i.LoadFromResource( d );
		return i;
	}

	private static ToolItem Make( ToolDefinition d )
	{
		var i = new ToolItem();
		i.LoadFromResource( d );
		return i;
	}

	private static BlueprintItem Make( BlueprintDefinition d )
	{
		var i = new BlueprintItem();
		i.LoadFromResource( d );
		return i;
	}

	/// <summary>
	/// Convenience: load a definition by file path and instantiate.
	/// Returns null if path doesn't resolve to an ItemDefinition.
	/// </summary>
	public static InventoryItem CreateFromPath( string assetPath, int stackCount = 1 )
	{
		var def = ResourceLibrary.Get<ItemDefinition>( assetPath );
		return def is null ? null : CreateFromAsset( def, stackCount );
	}
}
