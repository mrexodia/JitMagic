# JitMagic

JitMagic is a tool that allows you to have multiple Just-In-Time debuggers at once.  It is able to also pass JIT operations off to other JIT debuggers like Visual Studio's JIT choice form.  There should not be any functionality loss with a debugger by switching to JitMagic (full AeDebug featureset and eventing supported).

![Screenshot](https://raw.githubusercontent.com/mrexodia/JitMagic/master/screenshot.png "Screenshot")

[![](https://github.com/mrexodia/JitMagic/workflows/continuous/badge.svg)](https://github.com/mrexodia/JitMagic/actions/workflows/continuous.yml?query=branch%3Amaster)
<!-- MarkdownTOC -->

- [Features](#features)
- [Installation](#installation)
- [Configuration](#configuration)
	- [Debugger Configuration Structure](#debugger-configuration-structure)
- [Removal](#removal)
- [For Debugger Developers](#for-debugger-developers)
	- [Adding your debugger to JitMagic](#adding-your-debugger-to-jitmagic)
	- [How your debugger should behave](#how-your-debugger-should-behave)
	- [Technical details](#technical-details)

<!-- /MarkdownTOC -->

## Features
- Support an unlimited list of user customizable debuggers
- Debuggers can be architecture specific (x86,x64, or both) in terms of what apps they can debug (and will only be offered when appropriate) 
- Pass through JIT event signaling for same-as-native JIT operations (no function loss using JitMagic)
- Optional delay assistance for debugger applications that may not fully support normal JIT debugging

## Installation

Run JitMagic.exe it will check if it is the registered debugger and if not it will prompt to update the system JIT debugger it itself.  It will backup the existing debugger. 

## Configuration

JitMagic configuration is stored in the `JitMagic.json` file that is found next to the executable.  If it does not exist it is created on first run.  Some of the configuration features are exposed in the UI but most must be manually configured in the JSON file.   It comes pre-populated with some standard debuggers, but it expects them at their normal locations.  If they are not found they will not show up in the list. To specify your own or update the paths for any pre-populated ones just edit the JSON file with a text editor (or using an online editor like [https://jsoneditoronline.org/]).   Some features like blacklisting specific applications may be possible to add to the config using the UI but only possible to remove by editing the JSON file.

### Debugger Configuration Structure
A json entry for a debugger looks like:
```json
{
	"Name": "dnSpy",
	"Architecture": "x64",
	"FileName": "c:\\Program Files\\dnSpy\\dnSpy.exe",
	"Arguments": "--dont-load-files --multiple -p {pid} -e {debugSignalFd} --jdinfo {jitDebugInfoPtr}",
	"IconOverridePath": "c:\\windows\\System32\\SHELL32.dll,5",
	"AdditionalDelaySecs": 0
}
```
Most fields are self explanatory.
- `IconOverridePath` is the path to a .ico file or a path to an exe/dll to extract the icon from.  For an exe/dll you can optionally specify the index into the file (the `,5` above) for which icon other than the default to select.
- `AdditionalDelaySecs` seconds before JitMagic would normally exit that it will wait (when it exits it also signals the system to resume the process). This can be useful for debuggers that signal to a parent process and need time to attach but do not support the debug event signaler. 


## Removal
Run `JitMigic.exe --unregister` or launch JitMigic without any command line args and select "Remove as JIT".  JitMagic will restore the system debugger to the one that existed when it was installed (or nothing if there wasn't one).

## For Debugger Developers
### Adding your debugger to JitMagic

Do you have a debugger you want JitMagic to offer? Great. For the most part if your app can already be used as a native AeDebug app it should work seamlessly with JitMagic.  If your app does not support AeDebug style debugging already see [How your debugger should behave](#how-your-debugger-should-behave) below for details on how it may still work.  The recommend way of registering yourself with JitMagic is to check the AeDebug Debugger key `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug\Debugger` if it contains JitMagic.exe offer your users the option to add the debugger to JitMagic.

If they want to proceed take the executable path for JitMagic.exe (from the Debugger registry key you can extract its path similar to
```csharp
string debuggerVal = AeDebugReg.GetValue("Debugger");
string JitMagicPath = debuggerVal.substring(0,debuggerVal.indexOf("JitMagic.exe")).replace("\"","");
```
and run it with the following command line options:

`JigMagic.exe --add-debugger "[DebuggerName]" "[DebuggerPath]" "[DebuggerArgs]" [x86|x64|All] [AdditionalDelaySecs(optional)]`

for example:

`JigMagic.exe --add-debugger "MyDebugger (x64)" "c:/WinDbg/MyDebugger.exe" "--pid {pid} --event {debugSignalFd} --jitPtr 0x{jitDebugInfoPtr}" x64 3`

Names should be unique.  If your debugger is different for x86 vs x64 just add the architecture to the name.  If a debugger entry already exists with that name it is updated with the options passed.  It is possible to edit the JSON file directly but that is not recommended as the format may change.

### How your debugger should behave
There is nothing special JitMagic requires for debuggers but below are some general notes for how WIndows automatic debuggers should work.

Like native automatic debugging there are 3 parameters JitMagic can pass to your application that you can use in your arg string:
- The process pid of the target to debug `{0}` or `{pid}`
- The file descriptor that points to the event handle you should use to signal when you are ready for the process to resume `{1}` or `{debugSignalFd}`
- The pointer address to the JIT_DEBUG_INFO in the in the target’s address space `{2}` or `{jitDebugInfoPtr}`

You can use [SetEvent](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-setevent) to signal the event handle.  Note you are signalling JitMagic not the original event, we then signal the original event shortly before we exit after getting your signal.  If your app exits we will automatically signal this event even if you have not told us to do so.  To assist apps that do not want (or cannot) use the event signal we support an optional "AdditionalDelaySecs" variable for each debugger in which we will defer signalling for X seconds after your app exits.  This may be particularly useful if you have a singleton instance of your app running and the instance of the app the debugger launches exits after telling the main instance what to debug (and you don't want to manually have it signal).  There is generally no downside to delaying the signal except for the longer delay for the user before the app resumes.  If you signal too quickly or exit before fully attached the app may resume before you are ready for it to do so.

Technically JitMagic.exe will work with normal AeDebug arg strings too so instead of `-p {pid} -e {debugSignalFd} -j 0x{jitDebugInfoPtr}` you can use a normal registry string like `-p %ld -e %ld -j 0x%p` but we are not using printf (just the c# string.format) so you cannot change the formatters away from `%ld` / `%p` it is simply offered as a convenience.

### Technical details

By default JitMagic.exe registers itself as the automated debugger for the system.  This follows standard Microsoft practices, see [Configuring Automatic Debugging](https://docs.microsoft.com/en-us/windows/desktop/debug/configuring-automatic-debugging) for more details.  You should not need to manually launch JitMagic to start debugging, but if you wanted to it expects to be called in the form of `JitMagic.exe -p %ld -e %ld -j %p` where those are the standard AeDebug parameters.  You can suppress the JitMagic.exe prompt to be registered as a system debugger with the variable in the JSON file.

