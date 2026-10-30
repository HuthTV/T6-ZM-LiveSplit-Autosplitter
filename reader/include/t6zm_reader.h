#pragma once
#include <stdint.h>

#ifdef T6R_BUILD
#define T6R_API extern "C" __declspec(dllexport)
#else
#define T6R_API extern "C" __declspec(dllimport)
#endif

#define T6R_API_VERSION 5
#define T6R_MAX_WATCHES 16

enum t6r_status {
    T6R_NO_PROCESS = 0,
    T6R_UNSUPPORTED = 1,
    T6R_NO_LEVEL = 2,
    T6R_RESOLVING = 3,
    T6R_READY = 4,
};

enum t6r_profile {
    T6R_PROFILE_SONGS = 0,
    T6R_PROFILE_ROUNDS = 1,
    T6R_PROFILE_PACK_A_PUNCH = 2,
    T6R_PROFILE_BUILDABLES = 3,
    T6R_PROFILE_RAINBOW = 4,
    T6R_PROFILE_EASTER_EGG = 5,
};

#pragma pack(push, 4)
typedef struct t6r_sample {
    uint32_t status;
    uint32_t clock;
    uint32_t generation;
    uint32_t watch_known;
    uint32_t watch_set;
    uint32_t retries;
    int32_t values[T6R_MAX_WATCHES];
    char map[32];
} t6r_sample;
#pragma pack(pop)

T6R_API int t6r_api_version(void);
T6R_API void t6r_poll(uint32_t profile, t6r_sample* out);
T6R_API void t6r_close(void);
