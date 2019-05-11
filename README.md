# JitMagic

JitMagic is a simple tool that allows you to have multiple Just-In-Time debuggers at once.

![screenshot](https://i.imgur.com/or4y3UK.png)

## Installation

You have to set the following registry keys:

```
HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\AeDebug\Debugger
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug\Debugger
```

To use `JitMagic.exe`:

```
C:\Projects\JitMagic\JitMagic\JitMagic\bin\Debug\JitMagic.exe" -p %ld -e %ld
```

See [Configuring Automatic Debugging](https://docs.microsoft.com/en-us/windows/desktop/debug/configuring-automatic-debugging) for more details.

## vsjitdebugger

To use this with Visual Studio's JIT debugger you need a special hook to make the Visual Studio version selection dialog work properly. For some reason `vsjitdebugger.exe` will check the contents of the `Debugger\AeDebug` key and if it doesn't point to itself it will not display running Visual Studio instances.

You can use [AppInitHook](https://github.com/mrexodia/AppInitHook) (module `WefaultMagic` injected to `werfault.exe` and `taskmsg.exe`) to get this to work.