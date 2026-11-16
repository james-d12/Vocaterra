namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>One cell of the equipment panel's paper doll (I04): where a slot stands in the doll's grid, and the Spanish noun that names it.</summary>
/// <param name="Flat">The slot's place in the flat list of every slot (<see cref="EquipmentData.FlatIndex"/>): the number a <c>SlotMove</c> carries as its slot.</param>
/// <param name="Row">The cell's row in the doll's grid, from 0 (the body rows first, then the counted groups').</param>
/// <param name="Column">The cell's column in the doll's grid, from 0.</param>
/// <param name="Label">The slot's Spanish label noun (<c>cabeza</c>), or <see langword="null"/> when the data gives none.</param>
public readonly record struct EquipmentCell(int Flat, int Row, int Column, string? Label);
