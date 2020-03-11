#ifdef _WIN32
#include <windows.h>
#include <stdio.h>
#else
#include <stdio.h>
#endif

int __cdecl main(int argc, char* argv[])
{
    printf("Hello world!");
    return 0;
}