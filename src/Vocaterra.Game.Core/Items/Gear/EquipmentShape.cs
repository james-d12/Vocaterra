namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The equipment panel's paper doll as a grid, read from <c>game/equipment.json</c> and nothing else (I04, inventory-plan 3.10). The groups with a <c>layer</c> are the columns, in layer order (never by id); the rows
/// are the body slots they name, in the order they first appear (<c>head</c>, <c>body</c>, <c>legs</c>, <c>feet</c>); a group that has no such slot leaves a hole. The groups with no layer (the accessories, the
/// off-hand, the backpack) follow below the body rows, in file order, each group on a new row unless its slots fit in what is left of the row before it. A seventh group is therefore a row of data: it gets its
/// cells here with no code edit.
/// </summary>
public sealed class EquipmentShape
{
    /// <summary>The least number of columns the counted groups flow into, so a doll with one layer still has a row of room.</summary>
    public const int MinColumns = 3;

    private EquipmentShape(IReadOnlyList<EquipmentCell> cells, IReadOnlyList<string?> rowLabels, IReadOnlyList<string?> columnHeadings, int columns, int bodyRows, int rows)
    {
        ColumnHeadings = columnHeadings;
        Cells = cells;
        RowLabels = rowLabels;
        Columns = columns;
        BodyRows = bodyRows;
        Rows = rows;
    }

    /// <summary>No groups: no doll.</summary>
    public static EquipmentShape Empty { get; } = new([], [], [], 0, 0, 0);

    /// <summary>Every cell, one per slot, in the flat order of the data.</summary>
    public IReadOnlyList<EquipmentCell> Cells { get; }

    /// <summary>The Spanish noun of each body row (its slots' label), or <see langword="null"/> for a row whose slots have none.</summary>
    public IReadOnlyList<string?> RowLabels { get; }

    /// <summary>The Spanish noun heading each column (the layered group's <c>heading</c>), or <see langword="null"/> for a column with none; one entry per layered column, so fewer than <see cref="Columns"/> when counted groups need more room (I04b).</summary>
    public IReadOnlyList<string?> ColumnHeadings { get; }

    /// <summary>Whether any column has a heading to be written above it.</summary>
    public bool HasHeadings => ColumnHeadings.Any(static h => h is not null);

    /// <summary>The number of columns of the grid.</summary>
    public int Columns { get; }

    /// <summary>The number of body rows (the layered groups' slots).</summary>
    public int BodyRows { get; }

    /// <summary>The number of rows in all: the body rows and the rows of the counted groups.</summary>
    public int Rows { get; }

    /// <summary>Whether any body row has a Spanish noun to be written beside it.</summary>
    public bool HasLabels => RowLabels.Any(static l => l is not null);

    /// <summary>Whether the data has no slot at all.</summary>
    public bool IsEmpty => Cells.Count == 0;

    /// <summary>Lays the data's slots out.</summary>
    /// <param name="data">The equipment data.</param>
    public static EquipmentShape Of(EquipmentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.SlotCount == 0)
        {
            return Empty;
        }

        var layered = Enumerable.Range(0, data.Groups.Count)
            .Where(g => data.Groups[g].Layer is not null && data.Groups[g].Slots.Count > 0)
            .OrderBy(g => data.Groups[g].Layer)
            .ThenBy(static g => g)
            .ToList();
        var names = new List<string>();
        foreach (var g in layered)
        {
            foreach (var slot in data.Groups[g].Slots.Where(slot => !names.Contains(slot.Name, StringComparer.Ordinal)))
            {
                names.Add(slot.Name);
            }
        }

        var cells = new List<EquipmentCell>();
        var labels = new string?[names.Count];
        for (var column = 0; column < layered.Count; column++)
        {
            var g = layered[column];
            for (var i = 0; i < data.Groups[g].Slots.Count; i++)
            {
                var spec = data.Groups[g].Slots[i];
                var row = names.IndexOf(spec.Name);
                labels[row] ??= spec.Label;
                cells.Add(new EquipmentCell(data.FlatIndex(new EquipSlot(g, i)), row, column, spec.Label));
            }
        }

        var columns = Math.Max(MinColumns, layered.Count);
        var nextRow = names.Count;
        var nextColumn = 0;
        for (var g = 0; g < data.Groups.Count; g++)
        {
            var group = data.Groups[g];
            if (group.Layer is not null || group.Slots.Count == 0)
            {
                continue;
            }

            if (nextColumn > 0 && (group.Slots.Count > columns - nextColumn))
            {
                nextRow++;
                nextColumn = 0;
            }

            for (var i = 0; i < group.Slots.Count; i++)
            {
                if (nextColumn == columns)
                {
                    nextRow++;
                    nextColumn = 0;
                }

                cells.Add(new EquipmentCell(data.FlatIndex(new EquipSlot(g, i)), nextRow, nextColumn, group.Slots[i].Label));
                nextColumn++;
            }
        }

        var rows = nextColumn > 0 ? nextRow + 1 : nextRow;
        return new EquipmentShape([.. cells.OrderBy(static c => c.Flat)], labels, [.. layered.Select(g => data.Groups[g].Heading)], columns, names.Count, rows);
    }
}
