/* SPDX-License-Identifier: GPL-2.0-or-later */
#include <stdio.h>
static volatile unsigned char payload[131072] = { 1, 2, 3 };
int main(void) {
    unsigned long total = 0;
    unsigned int index;
    for (index = 0; index < sizeof(payload); ++index) total += payload[index];
    printf("UPX Drop native fixture: %lu\n", total);
    return total == 6 ? 0 : 1;
}
