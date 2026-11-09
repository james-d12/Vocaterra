namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// A speaker button on the right page (U07): where the layer drew it this frame and the recording it plays. A lesson's phrases are written on the right page, wrapped and
/// scrolled by the layer, so the layer says where each button is and the mouse hits the same rectangle (<see cref="BookModel.RightButtons"/>).
/// </summary>
/// <param name="Rect">The button's rectangle on the canvas.</param>
/// <param name="Speak">The line id of the recording it plays.</param>
public sealed record BookRightButton(BookRect Rect, string Speak);
