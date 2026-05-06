// Vendored from conna.inventory (MIT, by conna).
// Renamed namespace Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// Source: https://github.com/conna-evil/sbox-inventory (MIT)

namespace SboxPro.Inventory;

public enum InventoryResult
{
	Success,
	ItemWasNull,
	ItemAlreadyInInventory,
	ItemNotInInventory,
	DestinationWasNull,
	InsertNotAllowed,
	RemoveNotAllowed,
	TransferNotAllowed,
	ReceiveNotAllowed,
	PlacementNotAllowed,
	StackingNotAllowed,
	InvalidStackCount,
	NoSpaceAvailable,
	SlotSizeMismatch,
	PlacementOutOfBounds,
	PlacementCollision,
	AmountMustBePositive,
	AmountExceedsStack,
	ItemNotStackable,
	CannotCombineWithSelf,
	BothItemsMustBeInInventory,
	DestinationStackFull,
	NoAuthority,
	RequestTimeout
}
