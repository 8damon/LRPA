#ifndef LRPA_H
#define LRPA_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#  if defined(LRPA_BUILD)
#    define LRPA_API __declspec(dllexport)
#  else
#    define LRPA_API __declspec(dllimport)
#  endif
#  define LRPA_CALL __cdecl
#else
#  define LRPA_API
#  define LRPA_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define LRPA_ABI_VERSION 1u
#define LRPA_OPTION_REMOVE_OWNED_ON_CLOSE 0x00000001u

#define LRPA_INPUT_MOUSE 0u
#define LRPA_INPUT_KEYBOARD 1u
#define LRPA_INPUT_HARDWARE 2u

#define LRPA_MOUSEEVENTF_MOVE 0x0001u
#define LRPA_MOUSEEVENTF_LEFTDOWN 0x0002u
#define LRPA_MOUSEEVENTF_LEFTUP 0x0004u
#define LRPA_MOUSEEVENTF_RIGHTDOWN 0x0008u
#define LRPA_MOUSEEVENTF_RIGHTUP 0x0010u
#define LRPA_MOUSEEVENTF_MIDDLEDOWN 0x0020u
#define LRPA_MOUSEEVENTF_MIDDLEUP 0x0040u
#define LRPA_MOUSEEVENTF_XDOWN 0x0080u
#define LRPA_MOUSEEVENTF_XUP 0x0100u
#define LRPA_MOUSEEVENTF_WHEEL 0x0800u
#define LRPA_MOUSEEVENTF_HWHEEL 0x1000u
#define LRPA_MOUSEEVENTF_MOVE_NOCOALESCE 0x2000u
#define LRPA_MOUSEEVENTF_VIRTUALDESK 0x4000u
#define LRPA_MOUSEEVENTF_ABSOLUTE 0x8000u

typedef uintptr_t lrpa_handle;

typedef enum lrpa_result {
    LRPA_OK = 0,
    LRPA_INVALID_ARGUMENT = 1,
    LRPA_INVALID_HANDLE = 2,
    LRPA_NOT_SUPPORTED = 3,
    LRPA_PROTOCOL_ERROR = 4,
    LRPA_NATIVE_ERROR = 5,
    LRPA_INTERNAL_ERROR = 6,
    LRPA_BUFFER_TOO_SMALL = 7
} lrpa_result;

typedef struct lrpa_options {
    uint32_t struct_size;
    uint32_t flags;
    int32_t creation_delay_ms;
    uint32_t reserved;
    const char* device_path_utf8;
    const char* driver_path_utf8;
} lrpa_options;

typedef struct lrpa_mouse_input {
    int32_t dx;
    int32_t dy;
    uint32_t mouse_data;
    uint32_t flags;
    uint32_t time;
    uintptr_t extra_info;
} lrpa_mouse_input;

typedef union lrpa_input_union {
    lrpa_mouse_input mouse;
    uint8_t reserved[32];
} lrpa_input_union;

typedef struct lrpa_input {
    uint32_t type;
    lrpa_input_union data;
} lrpa_input;

#pragma pack(push, 1)
typedef struct lrpa_mouse_report {
    uint16_t buttons;
    int16_t x;
    int16_t y;
    int8_t wheel;
    int8_t horizontal_pan;
} lrpa_mouse_report;
#pragma pack(pop)

LRPA_API uint32_t LRPA_CALL lrpa_abi_version(void);
LRPA_API size_t LRPA_CALL lrpa_options_size(void);
LRPA_API size_t LRPA_CALL lrpa_input_size(void);
LRPA_API size_t LRPA_CALL lrpa_mouse_report_size(void);

LRPA_API lrpa_result LRPA_CALL lrpa_open(
    const lrpa_options* options,
    lrpa_handle* out_handle,
    int32_t* out_created);

LRPA_API lrpa_result LRPA_CALL lrpa_close(lrpa_handle handle);

LRPA_API lrpa_result LRPA_CALL lrpa_send_input(
    lrpa_handle handle,
    uint32_t input_count,
    const lrpa_input* inputs,
    size_t input_size,
    uint32_t* out_sent);

LRPA_API lrpa_result LRPA_CALL lrpa_move(lrpa_handle handle, int32_t x, int32_t y);

LRPA_API lrpa_result LRPA_CALL lrpa_submit_report(
    lrpa_handle handle,
    const lrpa_mouse_report* report);

LRPA_API lrpa_result LRPA_CALL lrpa_last_error(
    char* buffer,
    size_t capacity,
    size_t* out_required);

#ifdef __cplusplus
}

static_assert(sizeof(lrpa_options) == 32, "lrpa_options ABI mismatch");
static_assert(sizeof(lrpa_input) == 40, "lrpa_input ABI mismatch");
static_assert(sizeof(lrpa_mouse_report) == 8, "lrpa_mouse_report ABI mismatch");
#elif defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
_Static_assert(sizeof(lrpa_options) == 32, "lrpa_options ABI mismatch");
_Static_assert(sizeof(lrpa_input) == 40, "lrpa_input ABI mismatch");
_Static_assert(sizeof(lrpa_mouse_report) == 8, "lrpa_mouse_report ABI mismatch");
#endif

#endif
