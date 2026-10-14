#include "lrpa.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int main(void)
{
    if (lrpa_abi_version() != LRPA_ABI_VERSION ||
        lrpa_options_size() != sizeof(lrpa_options) ||
        lrpa_input_size() != sizeof(lrpa_input) ||
        lrpa_mouse_report_size() != sizeof(lrpa_mouse_report)) {
        fputs("ABI version or size mismatch\n", stderr);
        return 1;
    }

    lrpa_options options = {0};
    lrpa_handle unexpected_handle = 0;
    options.struct_size = 1;
    if (lrpa_open(&options, &unexpected_handle, NULL) != LRPA_INVALID_ARGUMENT) {
        fputs("short options structure was not rejected\n", stderr);
        return 1;
    }

    options.struct_size = sizeof(options);
    options.reserved = 1;
    if (lrpa_open(&options, &unexpected_handle, NULL) != LRPA_INVALID_ARGUMENT) {
        fputs("reserved option field was not rejected\n", stderr);
        return 1;
    }

    if (lrpa_move(0, 1, 1) != LRPA_INVALID_HANDLE) {
        fputs("invalid handle was not rejected\n", stderr);
        return 1;
    }

    size_t required = 0;
    if (lrpa_last_error(NULL, 0, &required) != LRPA_BUFFER_TOO_SMALL || required <= 1) {
        fputs("error-size query failed\n", stderr);
        return 1;
    }

    char* message = (char*)malloc(required);
    if (message == NULL)
        return 1;
    lrpa_result result = lrpa_last_error(message, required, &required);
    int valid = result == LRPA_OK && strstr(message, "invalid or closed") != NULL;
    free(message);

    if (!valid) {
        fputs("thread-local error retrieval failed\n", stderr);
        return 1;
    }

    puts("LRPA ABI smoke test passed");
    return 0;
}
