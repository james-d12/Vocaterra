namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// Where the cells of a conjugation table sit on the right page (S28): a label column for the persons, then one equal column per tense, a header row and five
/// person rows. The layer draws at these rectangles and the tests check them against the page, so no cell runs off its page at any window size. Plain values.
/// </summary>
public static class ConjugationLayout
{
    /// <summary>The width of the column of persons (yo ... ustedes/ellos), in pixels.</summary>
    public const int LabelWidth = 112;

    /// <summary>The narrowest a tense column is laid out (X08): a form like <c>necesitaste</c> is about 70 px.</summary>
    public const int MinCellWidth = 72;

    /// <summary>
    /// How many tense columns stand side by side in one block of the table: as many as keep every column at <see cref="MinCellWidth"/> (at least one). A table with more tenses than
    /// that stacks them in blocks, one under another, each with its own header row, and the right page scrolls over them (X03).
    /// </summary>
    /// <param name="width">The width of the area the table is written in.</param>
    /// <param name="columns">The number of tenses.</param>
    public static int ColumnsPerBlock(int width, int columns) => Math.Clamp((width - LabelWidth) / MinCellWidth, 1, Math.Max(1, columns));

    /// <summary>The height of the header row and of each person row, in pixels.</summary>
    public const int RowHeight = 34;

    /// <summary>How many person rows a table has.</summary>
    public const int PersonRows = 5;

    /// <summary>The height of the whole table: the header and the person rows.</summary>
    public static int Height => RowHeight * (PersonRows + 1);

    /// <summary>The width of one tense's column.</summary>
    /// <param name="width">The width of the area the table is written in.</param>
    /// <param name="columns">The number of tense columns (1 to 3).</param>
    public static int ColumnWidth(int width, int columns) => (width - LabelWidth) / Math.Max(1, columns);

    /// <summary>The rectangle of a tense's heading.</summary>
    /// <param name="left">The left edge of the area.</param>
    /// <param name="top">The top of the table.</param>
    /// <param name="width">The width of the area.</param>
    /// <param name="columns">The number of tense columns.</param>
    /// <param name="column">The column, from 0.</param>
    public static BookRect Header(int left, int top, int width, int columns, int column) =>
        new(left + LabelWidth + (column * ColumnWidth(width, columns)), top, ColumnWidth(width, columns), RowHeight);

    /// <summary>The rectangle of a person's label (yo, tú, ...).</summary>
    /// <param name="left">The left edge of the area.</param>
    /// <param name="top">The top of the table.</param>
    /// <param name="row">The person row, from 0.</param>
    public static BookRect Label(int left, int top, int row) => new(left, top + ((row + 1) * RowHeight), LabelWidth - 6, RowHeight);

    /// <summary>The rectangle of a cell.</summary>
    /// <param name="left">The left edge of the area.</param>
    /// <param name="top">The top of the table.</param>
    /// <param name="width">The width of the area.</param>
    /// <param name="columns">The number of tense columns.</param>
    /// <param name="row">The person row, from 0.</param>
    /// <param name="column">The tense column, from 0.</param>
    public static BookRect Cell(int left, int top, int width, int columns, int row, int column) =>
        new(left + LabelWidth + (column * ColumnWidth(width, columns)), top + ((row + 1) * RowHeight), ColumnWidth(width, columns), RowHeight);
}
