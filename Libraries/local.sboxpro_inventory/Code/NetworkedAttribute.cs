// Vendored from conna.inventory (MIT, by conna).
// Namespace renamed Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// See LICENSE-NOTICES.md at sbox-pro repo root for full attribution.

﻿using System;
using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Properties belonging to an <see cref="InventoryItem"/> that have this attribute will be automatically
/// synchronized with subscribers of its parent <see cref="BaseInventory"/> whenever the value changes.
///	<p><b>The property setter must be public.</b></p>
/// </summary>
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapPropertySet, "SboxPro.Inventory.InventoryItem.OnNetworkedPropertySet" )]
[AttributeUsage( AttributeTargets.Property )]
public class NetworkedAttribute : Attribute
{

}
