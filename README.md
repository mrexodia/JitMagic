# JitMagic

JitMagic is a simple tool that allows you to have multiple Just-In-Timer debuggers at once.

![screenshot](https://i.imgur.com/or4y3UK.png)

## vsjitdebugger

To use this with Visual Studio's JIT debugger you need a special hook to make the Visual Studio version selection dialog work properly.

Adding `vsjitdebugger_hook.dll` to the import table of `vsjitdebuggerps.dll` should do the trick. Paths:

WOW64: `c:\Program Files (x86)\Common Files\Microsoft Shared\VS7Debug\vsjitdebuggerps.dll`

Native: `c:\Program Files\Common Files\microsoft shared\VS7Debug\vsjitdebuggerps.dll`