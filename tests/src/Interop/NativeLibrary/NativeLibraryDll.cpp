// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <xplatform.h>
#include <stdio.h>
#include <stdlib.h>

#ifndef WINAPI
#define WINAPI NATIVEAPI
#endif

#ifndef UNREFERENCED_PARAMETER
#define UNREFERENCED_PARAMETER(P)          (P)
#endif

#ifdef _WIN32
#define DLL_EXPORT_LINUX_ONLY
#define EXPORT_UNDECORATED_LINUX(FnName)
#define EXPORT_UNDECORATED_WINDOWS() __pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__))
#else
#define DLL_EXPORT_LINUX_ONLY DLL_EXPORT
#define EXPORT_UNDECORATED_LINUX(FnName) asm(FnName)
#define EXPORT_UNDECORATED_WINDOWS()
#endif

typedef enum
{
    FN_FunctionStdcall = 0,
    FN_FunctionCdecl,
    FN_WinapiWithBaseOnly,
    FN_WinapiWithBaseAndAnsiAndUnicode,
    FN_WinapiWithBaseAndAnsiAndUnicodeA,
    FN_WinapiWithBaseAndAnsiAndUnicodeW,
    FN_WinapiWithAnsiAndUnicodeA,
    FN_WinapiWithAnsiAndUnicodeW,
    FN_WinapiWithBaseAndUnicode,
    FN_WinapiWithBaseAndUnicodeW,
    FN_ExportedByNameAndOrdinal,
    FN_ExportedByOrdinalOnly
} FUNCTION_IDENTIFIER;

#define DECLARE_WINAPI_METHOD(FnName) \
    DLL_EXPORT_LINUX_ONLY \
    FUNCTION_IDENTIFIER WINAPI FnName() EXPORT_UNDECORATED_LINUX(FnName) { \
        EXPORT_UNDECORATED_WINDOWS(); \
        return FN_##FnName##; \
    }

extern "C" {

// {336202D6-53FC-4EC1-BAC3-DA0FFBCDAAA7} - randomly generated
DLL_EXPORT
GUID GlobalGuid = { 0x336202d6, 0x53fc, 0x4ec1,{ 0xba, 0xc3, 0xda, 0xf, 0xfb, 0xcd, 0xaa, 0xa7 } };

DLL_EXPORT
FUNCTION_IDENTIFIER __stdcall FunctionStdcall(GUID guid)
{
    UNREFERENCED_PARAMETER(guid);
    return FN_FunctionStdcall;
}

DLL_EXPORT
FUNCTION_IDENTIFIER __cdecl FunctionCdecl(GUID guid)
{
    UNREFERENCED_PARAMETER(guid);
    return FN_FunctionCdecl;
}

DECLARE_WINAPI_METHOD(WinapiWithBaseOnly);
DECLARE_WINAPI_METHOD(WinapiWithBaseAndAnsiAndUnicode);
DECLARE_WINAPI_METHOD(WinapiWithBaseAndAnsiAndUnicodeA);
DECLARE_WINAPI_METHOD(WinapiWithBaseAndAnsiAndUnicodeW);
DECLARE_WINAPI_METHOD(WinapiWithAnsiAndUnicodeA);
DECLARE_WINAPI_METHOD(WinapiWithAnsiAndUnicodeW);
DECLARE_WINAPI_METHOD(WinapiWithBaseAndUnicode);
DECLARE_WINAPI_METHOD(WinapiWithBaseAndUnicodeW);

#ifdef _WIN32

FUNCTION_IDENTIFIER __stdcall ExportedByNameAndOrdinal()
{
#pragma comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__ ",@100")
    return FN_ExportedByNameAndOrdinal;
}

FUNCTION_IDENTIFIER __stdcall ExportedByOrdinalOnly()
{
#pragma comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__ ",@200,NONAME")
    return FN_ExportedByOrdinalOnly;
}

#endif // _WIN32

} // extern "C"
