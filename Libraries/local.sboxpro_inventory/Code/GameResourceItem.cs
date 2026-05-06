// Vendored from conna.inventory (MIT, by conna).
// Namespace renamed Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// See LICENSE-NOTICES.md at sbox-pro repo root for full attribution.

﻿using System.Collections.Generic;
using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Base class for inventory items that are based from a <see cref="GameResource"/>.
/// </summary>
public class GameResourceItem<T> : InventoryItem where T : BaseItemAsset
{
	/// <summary>
	/// The <see cref="GameResource"/> that this item is based from.
	/// </summary>
	public T Resource { get; private set; }

	public override int MaxStackSize => Resource?.MaxStackSize ?? base.MaxStackSize;
	public override string DisplayName => Resource?.DisplayName ?? base.DisplayName;
	public override string Category => Resource?.Category ?? base.Category;
	public override int Width => Resource?.Width ?? base.Width;
	public override int Height => Resource?.Height ?? base.Height;

	/// <summary>
	/// Load data from the specified <see cref="GameResource"/>.
	/// </summary>
	public void LoadFromResource( T resource )
	{
		Resource = resource;
		OnResourceUpdated( resource );
	}

	/// <summary>
	/// Called when the <see cref="Resource"/> used for this item has been updated.
	/// </summary>
	protected virtual void OnResourceUpdated( T resource )
	{

	}

	public override void Serialize( Dictionary<string, object> data )
	{
		base.Serialize( data );

		// 🔄 [FORK] ResourceId is obsolete in current engine — store ResourcePath
		// (string) instead. Backward-compatible: Deserialize falls back to old key.
		data["ResourcePath"] = Resource?.ResourcePath ?? "";
	}

	public override void Deserialize( Dictionary<string, object> data )
	{
		base.Deserialize( data );

		if ( !data.TryGetValue( "ResourcePath", out var pathObj ) || pathObj is not string path || string.IsNullOrEmpty( path ) )
			return;

		Resource = ResourceLibrary.Get<T>( path );
		OnResourceUpdated( Resource );
	}
}
