using System.Runtime.InteropServices;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>
/// B46: the two defences against SDL's crash when it has no display left (the class's remarks give the core dumps). The crash itself cannot be made in a test (it is SDL's own
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
}
