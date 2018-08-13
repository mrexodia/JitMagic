#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include "minhook/include/MinHook.h"

LSTATUS (WINAPI* original_RegQueryValueExW)(
    HKEY                              hKey,
    LPCWSTR                           lpValueName,
    LPDWORD                           lpReserved,
    LPDWORD                           lpType,
    __out_data_source(REGISTRY)LPBYTE lpData,
    LPDWORD                           lpcbData
);

static LSTATUS WINAPI hook_RegQueryValueExW(
    HKEY                              hKey,
    LPCWSTR                           lpValueName,
    LPDWORD                           lpReserved,
    LPDWORD                           lpType,
    __out_data_source(REGISTRY)LPBYTE lpData,
    LPDWORD                           lpcbData
)
{
    if(lpValueName && wcscmp(lpValueName, L"Debugger") == 0)
    {
        OutputDebugStringW(L"[vsjitdebugger_hook] RegQueryValueExW -> Debugger");
        auto goodDebugger = L"\"C:\\Windows\\system32\\vsjitdebugger.exe\" -p %ld -e %ld";
        auto goodDebuggerSize = DWORD(wcslen(goodDebugger) * sizeof(wchar_t) + sizeof(wchar_t));
        if(!lpData && lpcbData)
        {
            *lpcbData = goodDebuggerSize;
            if(lpType)
                *lpType = REG_SZ;
            return ERROR_SUCCESS;
        }
        else if(lpData && lpcbData)
        {
            if(lpType)
                *lpType = REG_SZ;
            if(*lpcbData >= goodDebuggerSize)
            {
                wcscpy((wchar_t*)lpData, goodDebugger);
                return ERROR_SUCCESS;
            }
            else
            {
                *lpcbData = goodDebuggerSize;
                return ERROR_MORE_DATA;
            }
        }
    }
    return original_RegQueryValueExW(hKey, lpValueName, lpReserved, lpType, lpData, lpcbData);
}

extern "C" __declspec(dllexport) BOOL WINAPI DllMain(
    _In_ HINSTANCE hinstDLL,
    _In_ DWORD     fdwReason,
    _In_ LPVOID    lpvReserved
)
{
    if(fdwReason == DLL_PROCESS_ATTACH)
    {
        OutputDebugStringA("[vsjitdebugger_hook] DllMain");
        if(MH_Initialize() != MH_OK)
            OutputDebugStringA("[vsjitdebugger_hook] MH_Initialize failed!");
        if(MH_CreateHookApi(L"advapi32.dll", "RegQueryValueExW", hook_RegQueryValueExW, (LPVOID*)&original_RegQueryValueExW) != MH_OK)
            OutputDebugStringA("[vsjitdebugger_hook] MH_CreateHookApi (RegQueryValueExW) failed!");
        if(MH_EnableHook(MH_ALL_HOOKS) != MH_OK)
            OutputDebugStringA("[vsjitdebugger_hook] MH_EnableHook failed!");
    }
    return TRUE;
}