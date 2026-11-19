using System.Runtime.InteropServices;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>
/// B46, B48: the two defences against SDL's crash when it has no display left (the class's remarks give the core dumps). The crash itself cannot be made in a test (it is SDL's own
/// null read, after the X server's last output goes), so the tests pin the parts: the variable reaches the process's environment block, which SDL reads (not only .NET's copy), the
/// caller's own value wins.
/// </summary>
public sealed class SdlDisplaysTests
{
    [DllImport("libc", EntryPoint = "getenv")]
    private static extern IntPtr Native_getenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("libc", EntryPoint = "unsetenv")]
    private static extern int Native_unsetenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int Native_setenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);

    private static string? NativeValue(string name)
    {
        var pointer = Native_getenv(name);
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);
    }

    [Fact]
    public void Given_AnUnsetVariable_When_ItIsSetUnlessSet_Then_TheNativeEnvironmentHasItAndASecondValueDoesNotReplaceIt()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.False(SdlDisplays.SetEnvironmentUnlessSet("VOCATERRA_B46_TEST", "0"));
            return;
        }

        const string name = "VOCATERRA_B46_TEST";
        try
        {
            Assert.True(SdlDisplays.SetEnvironmentUnlessSet(name, "0"));
            Assert.Equal("0", NativeValue(name));
            Assert.False(SdlDisplays.SetEnvironmentUnlessSet(name, "1"));
            Assert.Equal("0", NativeValue(name));
        }
        finally
        {
            _ = Native_unsetenv(name);
        }
    }

    [Fact]
    public void Given_TheOwnersValue_When_RandRIsIgnored_Then_TheOwnersValueStands()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var before = NativeValue(SdlDisplays.RandRVariable);
        try
        {
            _ = Native_setenv(SdlDisplays.RandRVariable, "1", 1);
            Assert.False(SdlDisplays.IgnoreRandRChanges());
            Assert.Equal("1", NativeValue(SdlDisplays.RandRVariable));

            _ = Native_unsetenv(SdlDisplays.RandRVariable);
            Assert.True(SdlDisplays.IgnoreRandRChanges());
            Assert.Equal("0", NativeValue(SdlDisplays.RandRVariable));
        }
        finally
        {
            _ = before is null ? Native_unsetenv(SdlDisplays.RandRVariable) : Native_setenv(SdlDisplays.RandRVariable, before, 1);
        }
    }

    [Theory]
    [InlineData(true, null, null, true)] // a scripted run, whatever the session
    [InlineData(true, "x11", null, true)]
    [InlineData(false, "wayland", "wayland-0", true)] // B48: a GNOME session on Wayland, the game on Xwayland
    [InlineData(false, "wayland", null, true)]
    [InlineData(false, "Wayland", null, true)]
    [InlineData(false, null, "wayland-0", true)] // no session type (a bare terminal), the Wayland socket says it
    [InlineData(false, "tty", "wayland-0", true)]
    [InlineData(false, "x11", null, false)] // a plain X11 session keeps RandR: its multi-monitor and mode lists
    [InlineData(false, "x11", "wayland-0", false)] // the session type wins over a left-over variable
    [InlineData(false, null, null, false)]
    [InlineData(false, "", "", false)]
    public void Given_ASession_When_AskedWhetherToIgnoreRandR_Then_ScriptedAndWaylandRunsDo(bool scripted, string? sessionType, string? waylandDisplay, bool expected)
    {
        Assert.Equal(expected, SdlDisplays.ShouldIgnoreRandR(scripted, sessionType, waylandDisplay));
    }

    [Fact]
    public void Given_AWaylandSession_When_AnInteractiveRunStarts_Then_TheNativeEnvironmentSaysRandROffBeforeSdlStarts_AndAnX11SessionLeavesItAlone()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.False(SdlDisplays.IgnoreRandRChangesIfNeeded(false, "wayland", "wayland-0"));
            return;
        }

        var before = NativeValue(SdlDisplays.RandRVariable);
        try
        {
            _ = Native_unsetenv(SdlDisplays.RandRVariable);
            Assert.False(SdlDisplays.IgnoreRandRChangesIfNeeded(false, "x11", null));
            Assert.Null(NativeValue(SdlDisplays.RandRVariable));

            Assert.True(SdlDisplays.IgnoreRandRChangesIfNeeded(false, "wayland", "wayland-0"));
            Assert.Equal("0", NativeValue(SdlDisplays.RandRVariable));

            _ = Native_setenv(SdlDisplays.RandRVariable, "1", 1); // the player's own value wins, to turn this off
            Assert.False(SdlDisplays.IgnoreRandRChangesIfNeeded(false, "wayland", "wayland-0"));
            Assert.Equal("1", NativeValue(SdlDisplays.RandRVariable));
        }
        finally
        {
            _ = before is null ? Native_unsetenv(SdlDisplays.RandRVariable) : Native_setenv(SdlDisplays.RandRVariable, before, 1);
        }
    }
}
