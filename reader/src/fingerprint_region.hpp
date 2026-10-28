#pragma once
#include <cstdint>

namespace t6r::fingerprint {
struct region { std::uint64_t address; std::uint32_t size; const std::uint8_t* bytes; };
}
