#include <string.h>
#include <stdlib.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <sys/uio.h>
#include <asm/types.h>
#include "diagtrace.h"

char *diagtrace_register_page = NULL;
int diagtrace_data_fd = -1;

struct user_reg {
    __u32 size;
    __u64 name_args;
    __u32 status_index;
    __u32 write_index;
};

#define DIAG_IOC_MAGIC '*'
#define DIAG_IOCSREG _IOWR(DIAG_IOC_MAGIC, 0, struct user_reg*)
#define DIAG_IOCSDEL _IOW(DIAG_IOC_MAGIC, 1, char*)

int diagtrace_init_no_fallback(
    void)
{
    int page_size = sysconf(_SC_PAGESIZE);

    int fd = open("/sys/kernel/debug/tracing/user_events_status", O_RDWR);

    if (fd == -1)
        fd = open("/sys/kernel/tracing/user_events_status", O_RDWR);

    if (fd == -1)
    {
        printf("udiag: Failed opening status FD, %d\n", errno);
        return -errno;
    }

    diagtrace_data_fd = open("/sys/kernel/debug/tracing/user_events_data", O_RDWR);

    if (diagtrace_data_fd == -1)
        diagtrace_data_fd = open("/sys/kernel/debug/tracing/user_events_status", O_RDWR);

    if (diagtrace_data_fd == -1)
    {
        printf("udiag: Failed opening data FD, %d\n", errno);
        return -errno;
    }

    char *page = (char*)mmap(
        NULL,
        page_size,
        PROT_READ,
        MAP_SHARED,
        fd,
        0);

    close(fd);

    if (page == MAP_FAILED)
    {
        printf("mmap() failed, %d\n", errno);
        return -errno;
    }

    diagtrace_register_page = page;

    return 0;
}

int diagtrace_init(
    void)
{
    int ret;

    ret = diagtrace_init_no_fallback();

    if (ret < 0)
    {
        // fallback to empty page.
        int page_size = sysconf(_SC_PAGESIZE);

        diagtrace_register_page = (char*)calloc(page_size, 1);

        if (diagtrace_register_page)
        {
            ret = 0;
        }
        else
        {
            ret = -ENOMEM;
        }
    }

    return ret;
}

int diagtrace_register_provider(
    const char *provider,
    int *handle)
{
    int status;
    struct user_reg reg = {0};
    *handle = -1;

    reg.size = sizeof(reg);
    reg.name_args = (__u64)provider;

    status = ioctl(diagtrace_data_fd, DIAG_IOCSREG, &reg);

    if (status < 0)
    {
        // return nop provider (callers will never get enabled):
        // Caused by too many providers, etc.
        return 0;
    }

    *handle = reg.write_index;

    return reg.status_index;
}
