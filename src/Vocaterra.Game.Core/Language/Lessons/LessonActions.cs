namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>The lesson strip's keys and buttons as rules: which action a key presses, the key a button shows and which option a button picks.</summary>
public static class LessonActions
{
    /// <summary>The key a button answers to, as the button's text shows it.</summary>
    /// <param name="action">The button.</param>
    public static string KeyLabel(LessonAction action) => action switch
    {
        LessonAction.Start => "L",
        LessonAction.Continue => "Enter",
        LessonAction.Skip => "K",
        LessonAction.Help => "H",
        LessonAction.More => "M",
        LessonAction.Option1 => "1",
        LessonAction.Option2 => "2",
        LessonAction.Option3 => "3",
        _ => "4",
    };

    /// <summary>The lesson action a key presses, or <see langword="null"/>.</summary>
    /// <param name="key">The key.</param>
    public static LessonAction? ForKey(LessonKey key) => key switch
    {
        LessonKey.Enter => LessonAction.Continue,
        LessonKey.K => LessonAction.Skip,
        LessonKey.L => LessonAction.Start,
        LessonKey.D1 => LessonAction.Option1,
        LessonKey.D2 => LessonAction.Option2,
        LessonKey.D3 => LessonAction.Option3,
        LessonKey.D4 => LessonAction.Option4,
        LessonKey.M => LessonAction.More,
        _ => null,
    };

    /// <summary>The index of an option action (0 to 3), or <see langword="null"/> for the other buttons.</summary>
    /// <param name="action">The action.</param>
    public static int? OptionIndex(LessonAction action) => action switch
    {
        LessonAction.Option1 => 0,
        LessonAction.Option2 => 1,
        LessonAction.Option3 => 2,
        LessonAction.Option4 => 3,
        _ => null,
    };
}
