#include "lrpa.h"
#include <stdio.h>
#include <stdlib.h>

static void fail(lrpa_result result)
{
    size_t required = 0;
    lrpa_last_error(NULL, 0, &required);
    char* message = (char*)malloc(required);
    if (message != NULL && lrpa_last_error(message, required, &required) == LRPA_OK)
        fprintf(stderr, "LRPA error %d: %s\n", result, message);
    else
        fprintf(stderr, "LRPA error %d\n", result);
    free(message);
    exit(1);
}

int main(void)
{
    lrpa_handle handle = 0;
    int32_t created = 0;
    lrpa_result result = lrpa_open(NULL, &handle, &created);
    if (result != LRPA_OK)
        fail(result);

    lrpa_input input = {0};
    input.type = LRPA_INPUT_MOUSE;
    input.data.mouse.dx = 64;
    input.data.mouse.dy = -16;
    input.data.mouse.flags = LRPA_MOUSEEVENTF_MOVE;

    uint32_t sent = 0;
    result = lrpa_send_input(handle, 1, &input, sizeof(input), &sent);
    if (result != LRPA_OK)
        fail(result);

    printf("sent=%u created=%d\n", sent, created);
    result = lrpa_close(handle);
    if (result != LRPA_OK)
        fail(result);
    return 0;
}
