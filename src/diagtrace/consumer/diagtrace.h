#ifndef DIAGTRACE_H
#define DIAGTRACE_H
#include <stdio.h>
#include <errno.h>
#include <sys/mman.h>
#include <sys/uio.h>
#include <fcntl.h>

extern char *diagtrace_register_page;
extern int diagtrace_data_fd;

#ifndef UNREFERENCED_PARAMETER
#define UNREFERENCED_PARAMETER(x) (void)(x)
#endif

#define DIAG_INIT() diagtrace_init()
#define DIAG_PROVIDER_REG(name, handle) diagtrace_register_provider(name, handle)
#define DIAG_PROVIDER_ON(p) diagtrace_register_page[p]

//NOTE : do not update this maxsize without also editing CLogDiagTrace.cs
#define CLOG_EVENT_ID_MAXSIZE 48

struct CLOG_UDIAG_EVENT
{
    char clog_ID[CLOG_EVENT_ID_MAXSIZE];
};

#define DIAG_WRITE(provider, data, len) \
do { \
struct iovec io[2]; \
io[0].iov_base = &provider; \
io[0].iov_len = sizeof(provider); \
io[1].iov_base = data; \
io[1].iov_len = len; \
int __result = writev(diagtrace_data_fd, (const struct iovec*)io, 2); \
UNREFERENCED_PARAMETER(__result); \
} while (0);

#if defined(__cplusplus)
extern "C" {
#endif

int diagtrace_init_no_fallback(
    void);

int diagtrace_init(
    void);

int diagtrace_register_provider(
    const char *provider,
    int *handle);

#if defined(__cplusplus)
}
#endif

#endif /* DIAGTRACE_H */
