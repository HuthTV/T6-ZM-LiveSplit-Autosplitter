#define T6R_BUILD
#include "../include/t6zm_reader.h"
#include "fingerprint_plutonium.hpp"
#include "fingerprint_steam.hpp"
#include "fingerprint_store.hpp"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <tlhelp32.h>

#include <cstddef>
#include <iterator>
#include <cstring>
#include <string>
#include <string_view>
#include <unordered_map>

namespace {

using address_t = std::uint64_t;

struct build_def {
    const wchar_t* process;
    const t6r::fingerprint::region* regions;
    std::size_t region_count;
    std::uint32_t pointer_size;
    address_t vm_clock, level_root, objects_root, children_root, values_root, strings_root;
    bool names_from_operands;
    address_t field_names, field_name_limit;
    bool strings_indirect;
    address_t engine_time = 0;
};

constexpr build_def builds[] = {
    {L"plutonium-bootstrapper-win32.exe", t6r::fingerprint::plutonium::regions, std::size(t6r::fingerprint::plutonium::regions), 4,
     0x2DEA810, 0x2DEA81C, 0x2DEA400, 0x2DEA480, 0x2DEA580, 0x2BF2DA4, true, 0x5466C8, 0x51A9A3, true, 0x2338F18},
    {L"t6zm.exe", t6r::fingerprint::steam::regions, std::size(t6r::fingerprint::steam::regions), 4,
     0x2D3A910, 0x2D3A91C, 0x2D3A500, 0x2D3A580, 0x2D3A680, 0x2B42EA4, true, 0x7E1180, 0x7E115B, true},
    {L"t6zm.exe", t6r::fingerprint::store::regions, std::size(t6r::fingerprint::store::regions), 8,
     0x14249191C, 0x142491928, 0x142491500, 0x142491580, 0x142491680, 0x142283580, false, 0x142416E00, 0x34BC, false},
};

constexpr std::uint32_t object_limit = 0x8000;
constexpr std::uint32_t child_limit = 0x40000;
constexpr std::uint32_t chain_limit = 8192;

enum : std::uint8_t { tag_object = 1, tag_string = 2, tag_int = 7 };
enum : std::uint32_t { type_struct = 18, type_entity = 20, type_array = 21 };

constexpr ULONGLONG attach_interval_ms = 1000;
constexpr ULONGLONG resolve_interval_ms = 250;
constexpr int sample_attempts = 4;

struct child {
    std::uint32_t payload;
    std::uint8_t tag;
    std::uint32_t key;
    std::uint16_t owner;
    std::uint32_t next;
    std::uint32_t prev;
};

enum class watch_kind { flag, field };

struct watch_def {
    watch_kind kind;
    const char* name;
};

constexpr watch_def songs[] = {{watch_kind::flag, "initial_blackscreen_passed"}, {watch_kind::field, "meteor_counter"},
                               {watch_kind::field, "snd115count"}, {watch_kind::field, "found_ee_radio_count"},
                               {watch_kind::field, "music_override"}, {watch_kind::flag, "nixie_puzzle_solved"},
                               {watch_kind::field, ""} ,
                               {watch_kind::field, "mannequin_count"}, {watch_kind::field, "round_number"},
                               {watch_kind::flag, "activate_zone_nml"}};
constexpr std::uint32_t songs_optional_from = 1;
constexpr watch_def rounds[] = {{watch_kind::flag, "initial_blackscreen_passed"}, {watch_kind::field, "round_number"}, {watch_kind::field, "round_start_time"}};

struct map_wants {
    const char* map;
    std::uint32_t mask;
};

struct profile_def {
    const watch_def* watches;
    std::uint32_t count;
    const char* player_field = nullptr;
    bool built_set = false;
    const char* player_ints[2] = {};
    const char* player_array = nullptr;
    const char* array_key = nullptr;
    std::uint32_t optional_from = 32;
    const map_wants* wants = nullptr;
    bool ee = false;
};

constexpr std::uint32_t max_players = 4;
constexpr std::uint32_t player_values = 1;
constexpr std::uint32_t int_values = 1;
constexpr std::uint32_t array_value = 3;

constexpr watch_def pack_a_punch[] = {{watch_kind::flag, "initial_blackscreen_passed"}};

#if __has_include("t6r_extra_profiles.inc")
#include "t6r_extra_profiles.inc"
#else
#define T6R_EXTRA_PROFILES
#endif
constexpr watch_def buildables[] = {{watch_kind::flag, "initial_blackscreen_passed"}};
constexpr std::uint32_t built_value = 1;

constexpr watch_def rainbow[] = {{watch_kind::flag, "initial_blackscreen_passed"}};

constexpr map_wants song_wants[] = {
    {"zm_transit", 1u << 1},
    {"zm_nuked", (1u << 1) | (1u << 4) | (1u << 7) | (1u << 8)},
    {"zm_highrise", 1u << 1},
    {"zm_prison", (1u << 1) | (1u << 4) | (1u << 5)},
    {"zm_buried", 1u << 1},
    {"zm_tomb", (1u << 1) | (1u << 2) | (1u << 3) | (1u << 8) | (1u << 9)},
    {nullptr, 0},
};

constexpr profile_def profiles[] = {{songs, 10, nullptr, false, {}, nullptr, nullptr, songs_optional_from, song_wants}, {rounds, 3}, {pack_a_punch, 1, "pap_used"}, {buildables, 1, nullptr, true},
                                    {rainbow, 1, nullptr, false, {"num_perks", "player_perk_purchase_limit"}, "dig_vars", "has_upgraded_shovel"},
                                    {nullptr, 0, nullptr, false, {}, nullptr, nullptr, 32, nullptr, true} T6R_EXTRA_PROFILES};

struct built_map {
    const char* map;
    const char* array;
    const char* const* names;
    std::uint32_t count;
};

constexpr const char* transit_names[] = {"turbine", "riotshield_zm", "turret", "electric_trap", "powerswitch", "jetgun_zm", "pap", "cattlecatcher", "bushatch|dinerhatch", "busladder", "sq_common"};
constexpr const char* highrise_names[] = {"springpad_zm", "slipgun_zm", "sq_common"};
constexpr const char* buried_names[] = {"turbine", "headchopper_zm", "subwoofer_zm", "sq_common", "springpad_zm",
                                        "buried_sq_bt_m_tower|buried_sq_bt_r_tower", "buried_sq_oillamp", "buried_sq_tpo_switch"};
constexpr const char* prison_names[] = {"alcatraz_shield_zm", "plane", "packasplat"};
constexpr const char* tomb_names[] = {"equip_dieseldrone_zm", "tomb_shield_zm", "elemental_staff_fire", "elemental_staff_air", "elemental_staff_lightning", "elemental_staff_water"};
constexpr built_map built_maps[] = {
    {"zm_transit", "buildables_built", transit_names, static_cast<std::uint32_t>(std::size(transit_names))},
    {"zm_highrise", "buildables_built", highrise_names, static_cast<std::uint32_t>(std::size(highrise_names))},
    {"zm_buried", "buildables_built", buried_names, static_cast<std::uint32_t>(std::size(buried_names))},
    {"zm_prison", "craftables_crafted", prison_names, static_cast<std::uint32_t>(std::size(prison_names))},
    {"zm_tomb", "craftables_crafted", tomb_names, static_cast<std::uint32_t>(std::size(tomb_names))},
};
constexpr std::uint32_t max_watches = T6R_MAX_WATCHES;

struct watch_slot {
    std::uint32_t child = 0;
    std::uint32_t key = 0;
    std::uint32_t owner = 0;
    bool known = false;
};

struct player_track {
    std::uint32_t entity = 0;
    std::uint32_t head = 0;
    std::uint32_t bits = 0;
    std::uint32_t int_child[2] = {};
    std::uint32_t array_child = 0;
    std::unordered_map<std::uint32_t, std::uint32_t> known;
};

struct state {
    HANDLE process = nullptr;
    const build_def* build = nullptr;
    bool unsupported = false;
    ULONGLONG next_attach = 0;

    std::uint32_t profile = 0xFFFFFFFF;
    std::uint32_t level = 0;
    std::uint32_t generation = 0;
    std::uint32_t last_clock = 0;
    std::int64_t time_origin = INT64_MAX;
    address_t objects = 0, children = 0, values = 0, strings = 0;
    std::uint32_t flag_array = 0;
    ULONGLONG next_resolve = 0;
    bool complete = false;
    bool full = false;
    char map[32] = {};
    watch_slot watches[max_watches];

    address_t field_names = 0;
    std::uint32_t field_name_limit = 0;
    std::uint32_t players_child = 0, players_array = 0;
    std::uint32_t players_bounds[2] = {};
    std::uint32_t player_count = 0;
    const char* wanted[5] = {};
    std::uint32_t wanted_keys[5] = {};
    std::unordered_map<std::uint32_t, bool> array_keys;
    bool players_ok = false;
    player_track players[max_players];
    const built_map* set_map = nullptr;
    std::uint32_t set_child = 0, set_array = 0;
    ULONGLONG next_set = 0;
    std::unordered_map<std::uint32_t, int> set_keys;
} g;

bool read(address_t address, void* out, std::size_t size) {
    SIZE_T done = 0;
    return address >= 0x10000 && address <= UINTPTR_MAX && ReadProcessMemory(g.process, reinterpret_cast<LPCVOID>(static_cast<std::uintptr_t>(address)), out, size, &done) && done == size;
}

bool u32(address_t address, std::uint32_t& out) { return read(address, &out, 4); }

bool pointer(address_t address, address_t& out) {
    if (g.build->pointer_size == 8) return read(address, &out, 8);
    std::uint32_t narrow = 0;
    if (!u32(address, narrow)) return false;
    out = narrow;
    return true;
}

bool text(address_t address, char* out, std::size_t capacity) {
    std::size_t length = 0;
    while (length + 1 < capacity) {
        char chunk[16];
        std::size_t got = sizeof(chunk);
        if (!read(address + length, chunk, got)) {
            got = 1;
            if (!read(address + length, chunk, got)) return false;
        }
        for (std::size_t i = 0; i < got && length + 1 < capacity; ++i) {
            if (chunk[i] == 0) {
                out[length] = 0;
                return true;
            }
            if (chunk[i] < 32 || chunk[i] > 126) return false;
            out[length++] = chunk[i];
        }
    }
    return false;
}

bool text_equals(address_t address, std::string_view expected) {
    char buffer[64];
    return expected.size() < sizeof(buffer) && text(address, buffer, sizeof(buffer)) && expected == buffer;
}

bool read_child(std::uint32_t index, child& out) {
    std::uint8_t raw[28];
    if (index == 0 || index >= child_limit || !read(g.values + index * 28, raw, sizeof(raw))) return false;
    std::memcpy(&out.payload, raw, 4);
    out.tag = raw[12];
    std::uint16_t high = 0;
    std::memcpy(&high, raw + 16, 2);
    out.key = raw[13] + (static_cast<std::uint32_t>(high) << 8);
    std::memcpy(&out.owner, raw + 18, 2);
    std::memcpy(&out.next, raw + 20, 4);
    std::memcpy(&out.prev, raw + 24, 4);
    return true;
}

bool object_type(std::uint32_t id, std::uint32_t& type) {
    std::uint32_t word = 0;
    if (id == 0 || id >= object_limit || !u32(g.objects + id * 8 + 4, word)) return false;
    type = word & 0x7F;
    return true;
}

void close_process() {
    if (g.process) CloseHandle(g.process);
    const ULONGLONG next_attach = g.next_attach;
    const std::uint32_t generation = g.generation;
    g = state{};
    g.next_attach = next_attach;
    g.generation = generation;
}

bool fingerprint_matches(HANDLE process, const build_def& build) {
    for (const auto* region_it = build.regions; region_it != build.regions + build.region_count; ++region_it) {
        const auto& region = *region_it;
        if (region.address > UINTPTR_MAX) return false;
        std::uint8_t live[0x100];
        SIZE_T done = 0;
        if (region.size > sizeof(live) || !ReadProcessMemory(process, reinterpret_cast<LPCVOID>(static_cast<std::uintptr_t>(region.address)), live, region.size, &done) || done != region.size || std::memcmp(live, region.bytes, region.size) != 0) return false;
    }
    return true;
}

void attach() {
    const ULONGLONG now = GetTickCount64();
    if (now < g.next_attach) return;
    g.next_attach = now + attach_interval_ms;
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) return;
    g.unsupported = false;
    for (const build_def& build : builds) {
        if (build.vm_clock > UINTPTR_MAX) continue;
        PROCESSENTRY32W entry{sizeof(entry)};
        for (BOOL more = Process32FirstW(snapshot, &entry); more; more = Process32NextW(snapshot, &entry)) {
            if (_wcsicmp(entry.szExeFile, build.process) != 0) continue;
            HANDLE process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, entry.th32ProcessID);
            if (!process) continue;
            if (fingerprint_matches(process, build)) {
                g.process = process;
                g.build = &build;
                g.unsupported = false;
                CloseHandle(snapshot);
                return;
            }
            CloseHandle(process);
            g.unsupported = true;
        }
    }
    CloseHandle(snapshot);
}

void forget_players() {
    g.players_child = g.players_array = g.player_count = 0;
    for (auto& key : g.wanted_keys) key = 0;
    g.array_keys.clear();
    g.players_bounds[0] = g.players_bounds[1] = 0;
    g.players_ok = false;
    for (auto& player : g.players) player = player_track{};
}

void forget_level() {
    forget_players();
    g.flag_array = 0;
    g.complete = false;
    g.full = false;
    g.next_resolve = 0;
    g.map[0] = 0;
    g.set_map = nullptr;
    g.set_child = g.set_array = 0;
    g.next_set = 0;
    g.set_keys.clear();
    g.time_origin = INT64_MAX;
    for (auto& slot : g.watches) slot = watch_slot{};
}

std::uint32_t frame_tick(std::uint32_t clock, std::uint32_t engine_time) {
    const std::int64_t delta = std::int64_t{engine_time} - 50 * std::int64_t{clock};
    if (delta < g.time_origin) g.time_origin = delta;
    return delta - g.time_origin == 50 ? clock + 1 : clock;
}

void resolve(std::uint32_t level) {
    g.next_resolve = GetTickCount64() + resolve_interval_ms;
    address_t objects = 0, children = 0, values = 0, strings = 0, field_names = 0;
    std::uint32_t field_name_limit = 0;
    const build_def& build = *g.build;
    if (!pointer(build.objects_root, objects) || !pointer(build.children_root, children) || !pointer(build.values_root, values)) return;
    if (build.strings_indirect) {
        if (!pointer(build.strings_root, strings)) return;
    } else {
        strings = build.strings_root;
    }
    if (build.names_from_operands) {
        std::uint32_t table = 0;
        if (!u32(build.field_names, table) || !u32(build.field_name_limit, field_name_limit)) return;
        field_names = table;
    } else {
        field_names = build.field_names;
        field_name_limit = static_cast<std::uint32_t>(build.field_name_limit);
    }
    if (field_names < 0x10000 || field_name_limit <= 1 || field_name_limit > 0x10000) return;
    g.objects = objects, g.children = children, g.values = values, g.strings = strings;

    std::uint32_t type = 0;
    if (!object_type(level, type) || (type != type_struct && type != type_entity)) return;

    const profile_def& profile = profiles[g.profile];
    watch_slot found[max_watches] = {};
    std::uint32_t bounds[2] = {};
    if (!read(children + level * 8, bounds, sizeof(bounds))) return;
    std::uint32_t flag_array = 0, players_child = 0, players_array = 0, current = bounds[0], previous = 0, steps = 0;
    char map[32] = {};
    while (current != 0) {
        child c{};
        if (++steps > chain_limit || !read_child(current, c) || c.owner != level || c.prev != previous) return;
        address_t name_pointer = 0;
        char name[64];
        if (c.key > 0 && c.key < field_name_limit && pointer(field_names + c.key * build.pointer_size, name_pointer) && name_pointer != 0 && text(name_pointer, name, sizeof(name))) {
            if (c.tag == tag_object && std::strcmp(name, "flag") == 0) flag_array = c.payload;
            else if (c.tag == tag_object && std::strcmp(name, "players") == 0) players_child = current, players_array = c.payload;
            else if (c.tag == tag_string && std::strcmp(name, "script") == 0 && c.payload < child_limit) text(strings + c.payload * 24 + 4, map, sizeof(map));
            else if (c.tag == tag_int)
                for (std::uint32_t i = 0; i < profile.count; ++i)
                    if (profile.watches[i].kind == watch_kind::field && std::strcmp(name, profile.watches[i].name) == 0) found[i] = {current, c.key, level, true};
        }
        previous = current;
        current = c.next;
    }
    if (previous != bounds[1] || !flag_array || !object_type(flag_array, type) || type != type_array) return;

    if (!read(children + flag_array * 8, bounds, sizeof(bounds))) return;
    current = bounds[0], previous = 0, steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > chain_limit || !read_child(current, c) || c.owner != flag_array || c.prev != previous) return;
        if (c.key > 0 && c.key < child_limit) {
            char name[64];
            if (text(strings + c.key * 24 + 4, name, sizeof(name)))
                for (std::uint32_t i = 0; i < profile.count; ++i)
                    if (profile.watches[i].kind == watch_kind::flag && std::strcmp(name, profile.watches[i].name) == 0) found[i] = {current, c.key, flag_array, true};
        }
        previous = current;
        current = c.next;
    }
    std::uint32_t level_after = 0;
    address_t objects_after = 0;
    if (previous != bounds[1] || !u32(build.level_root, level_after) || level_after != level || !pointer(build.objects_root, objects_after) || objects_after != objects) return;

    g.flag_array = flag_array;
    g.field_names = field_names, g.field_name_limit = field_name_limit;
    g.players_child = players_child, g.players_array = players_array;
    std::memcpy(g.map, map, sizeof(map));
    g.complete = true;
    std::uint32_t wanted_mask = 0;
    if (profile.wants)
        for (const map_wants* wants = profile.wants; wants->map; ++wants)
            if (std::strcmp(map, wants->map) == 0) wanted_mask = wants->mask;
    bool wanted_known = true;
    for (std::uint32_t i = 0; i < profile.count; ++i) {
        g.watches[i] = found[i];
        if (i < profile.optional_from) g.complete = g.complete && found[i].known;
        if (wanted_mask & (1u << i)) wanted_known = wanted_known && found[i].known;
    }
    g.full = g.complete && wanted_known;
}

bool field_name(std::uint32_t key, char* out, std::size_t capacity) {
    address_t name_pointer = 0;
    return key > 0 && key < g.field_name_limit && pointer(g.field_names + key * g.build->pointer_size, name_pointer) && name_pointer != 0 && text(name_pointer, out, capacity);
}

std::uint32_t field_bits(std::uint32_t key) {
    std::uint32_t bits = 0;
    char name[64];
    bool named = false;
    for (int i = 0; i < 5; ++i) {
        if (!g.wanted[i]) continue;
        if (g.wanted_keys[i]) {
            if (key == g.wanted_keys[i]) bits |= 1u << i;
            continue;
        }
        if (!named) named = field_name(key, name, sizeof(name));
        if (named && std::strcmp(name, g.wanted[i]) == 0) {
            g.wanted_keys[i] = key;
            bits |= 1u << i;
        }
    }
    return bits;
}

constexpr const char* corpse_map = "zm_prison";
constexpr const char* corpse_field = "e_afterlife_corpse";
constexpr std::uint32_t corpse_value = 6;

bool select_player_fields() {
    g.wanted[0] = profiles[g.profile].player_field;
    g.wanted[1] = std::strcmp(g.map, corpse_map) == 0 ? corpse_field : nullptr;
    g.wanted[2] = profiles[g.profile].player_ints[0];
    g.wanted[3] = profiles[g.profile].player_ints[1];
    g.wanted[4] = profiles[g.profile].player_array;
    return g.wanted[0] || g.wanted[1] || g.wanted[2] || g.wanted[3] || g.wanted[4];
}

void note_field(player_track& player, std::uint32_t index, std::uint32_t key) {
    player.known[index] = key;
    const std::uint32_t bits = field_bits(key);
    player.bits |= bits;
    for (int i = 0; i < 2; ++i)
        if (bits & (4u << i)) player.int_child[i] = index;
    if (bits & 16u) player.array_child = index;
}

std::int32_t read_player_array_int(const player_track& player) {
    child field{};
    if (!player.array_child || !read_child(player.array_child, field) || field.owner != player.entity || field.tag != tag_object) return 0;
    std::uint32_t bounds[2] = {};
    if (!read(g.children + field.payload * 8, bounds, sizeof(bounds))) return 0;
    std::uint32_t current = bounds[0], previous = 0, steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > 32 || !read_child(current, c) || c.owner != field.payload || c.prev != previous) return 0;
        auto known = g.array_keys.find(c.key);
        if (known == g.array_keys.end()) {
            char name[64];
            const bool wanted = c.key < child_limit && text(g.strings + static_cast<address_t>(c.key) * 24 + 4, name, sizeof(name)) && std::strcmp(name, profiles[g.profile].array_key) == 0;
            known = g.array_keys.emplace(c.key, wanted).first;
        }
        if (known->second) return c.tag == tag_int ? static_cast<std::int32_t>(c.payload) : 0;
        previous = current;
        current = c.next;
    }
    return 0;
}

bool scan_player(player_track& player) {
    std::uint32_t bounds[2] = {};
    if (!read(g.children + player.entity * 8, bounds, sizeof(bounds))) return false;
    player.known.clear();
    player.bits = 0;
    player.int_child[0] = player.int_child[1] = player.array_child = 0;
    std::uint32_t current = bounds[0], previous = 0, steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > chain_limit || !read_child(current, c) || c.owner != player.entity || c.prev != previous) return false;
        note_field(player, current, c.key);
        previous = current;
        current = c.next;
    }
    player.head = bounds[0];
    return previous == bounds[1];
}

void resolve_players() {
    for (auto& player : g.players) player = player_track{};
    g.players_ok = false;
    g.player_count = 0;
    std::uint32_t type = 0;
    if (!g.players_array || !object_type(g.players_array, type) || type != type_array) return;
    if (!read(g.children + g.players_array * 8, g.players_bounds, sizeof(g.players_bounds))) return;
    std::uint32_t current = g.players_bounds[0], previous = 0, steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > 64 || !read_child(current, c) || c.owner != g.players_array || c.prev != previous) return;
        const std::uint32_t slot = c.key - 0x800000;
        if (c.tag == tag_object && slot < max_players) {
            g.players[slot].entity = c.payload;
            if (!scan_player(g.players[slot])) return;
        }
        ++g.player_count;
        previous = current;
        current = c.next;
    }
    g.players_ok = previous == g.players_bounds[1];
}

void track_players(t6r_sample& out) {
    child field{};
    if (!read_child(g.players_child, field) || field.owner != g.level || field.tag != tag_object || field.payload != g.players_array) {
        g.complete = false;
        g.next_resolve = 0;
        return;
    }
    std::uint32_t bounds[2] = {};
    if (!read(g.children + g.players_array * 8, bounds, sizeof(bounds))) return;
    if (!g.players_ok || bounds[0] != g.players_bounds[0] || bounds[1] != g.players_bounds[1]) {
        resolve_players();
        if (!g.players_ok) return;
    }
    for (std::uint32_t slot = 0; slot < max_players; ++slot) {
        player_track& player = g.players[slot];
        if (!player.entity) continue;
        std::uint32_t head[2] = {};
        if (!read(g.children + player.entity * 8, head, sizeof(head))) return;
        if (head[0] != player.head) {
            std::uint32_t current = head[0], steps = 0;
            for (; current != 0 && steps < 64; ++steps) {
                child c{};
                if (!read_child(current, c) || c.owner != player.entity) break;
                auto seen = player.known.find(current);
                if (seen != player.known.end() && seen->second == c.key) break;
                note_field(player, current, c.key);
                current = c.next;
            }
            if (steps >= 64 && !scan_player(player)) return;
            player.head = head[0];
        }
        if (g.wanted[0]) {
            out.values[player_values + slot] = (player.bits & 1u) ? 1 : 0;
            out.watch_known |= 1u << (player_values + slot);
        }
        if (g.wanted[1] && (player.bits & 2u)) out.values[corpse_value] |= 1 << slot;
        if (slot == 0)
            for (std::uint32_t i = 0; i < 2; ++i) {
                if (!g.wanted[2 + i]) continue;
                child field_value{};
                std::int32_t value = 0;
                if (player.int_child[i] && read_child(player.int_child[i], field_value) && field_value.owner == player.entity && field_value.tag == tag_int)
                    value = static_cast<std::int32_t>(field_value.payload);
                out.values[int_values + i] = value;
                out.watch_known |= 1u << (int_values + i);
            }
        if (slot == 0 && g.wanted[4]) {
            out.values[array_value] = read_player_array_int(player);
            out.watch_known |= 1u << array_value;
        }
    }
    if (g.wanted[0] || g.wanted[2] || g.wanted[3] || g.wanted[4]) {
        out.values[player_values + max_players] = static_cast<std::int32_t>(g.player_count);
        out.watch_known |= 1u << (player_values + max_players);
    }
    if (g.wanted[1]) out.watch_known |= 1u << corpse_value;
}

void resolve_set() {
    g.next_set = GetTickCount64() + resolve_interval_ms;
    g.set_map = nullptr;
    for (const built_map& candidate : built_maps)
        if (std::strcmp(g.map, candidate.map) == 0) g.set_map = &candidate;
    if (!g.set_map || !g.level) return;
    std::uint32_t bounds[2] = {};
    if (!read(g.children + g.level * 8, bounds, sizeof(bounds))) return;
    std::uint32_t current = bounds[0], previous = 0, steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > chain_limit || !read_child(current, c) || c.owner != g.level || c.prev != previous) return;
        char name[64];
        std::uint32_t type = 0;
        if (c.tag == tag_object && field_name(c.key, name, sizeof(name)) && std::strcmp(name, g.set_map->array) == 0) {
            if (object_type(c.payload, type) && type == type_array) {
                g.set_child = current, g.set_array = c.payload;
                g.set_keys.clear();
            }
            return;
        }
        previous = current;
        current = c.next;
    }
}

bool name_listed(const char* spec, const char* name) {
    const std::size_t length = std::strlen(name);
    for (const char* part = spec;;) {
        const char* end = std::strchr(part, '|');
        const std::size_t size = end ? static_cast<std::size_t>(end - part) : std::strlen(part);
        if (size == length && std::memcmp(part, name, size) == 0) return true;
        if (!end) return false;
        part = end + 1;
    }
}

void read_set(t6r_sample& out, std::uint32_t index) {
    if (!g.set_array) {
        if (GetTickCount64() >= g.next_set) resolve_set();
        if (!g.set_array) return;
    }
    child field{};
    if (!read_child(g.set_child, field) || field.owner != g.level || field.tag != tag_object || field.payload != g.set_array) {
        g.set_array = 0;
        return;
    }
    std::uint32_t bounds[2] = {};
    if (!read(g.children + g.set_array * 8, bounds, sizeof(bounds))) return;
    std::int32_t mask = 0;
    std::uint32_t current = bounds[0], previous = 0, steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > 64 || !read_child(current, c) || c.owner != g.set_array || c.prev != previous) return;
        auto known = g.set_keys.find(c.key);
        if (known == g.set_keys.end()) {
            int bit = -1;
            char name[64];
            if (c.key < child_limit && text(g.strings + static_cast<address_t>(c.key) * 24 + 4, name, sizeof(name)))
                for (std::uint32_t i = 0; i < g.set_map->count; ++i)
                    if (name_listed(g.set_map->names[i], name)) bit = static_cast<int>(i);
            known = g.set_keys.emplace(c.key, bit).first;
        }
        if (known->second >= 0 && c.tag == tag_int && c.payload != 0) mask |= 1 << known->second;
        previous = current;
        current = c.next;
    }
    if (previous != bounds[1]) return;
    out.values[index] = mask;
    out.watch_known |= 1u << index;
}

enum class ee_kind { flag, field, length, entity, defined, equals, path, player_int, player_has };

struct ee_watch {
    std::uint32_t index;
    ee_kind kind;
    const char* name;
    const char* key1;
    const char* key2;
};

struct ee_map_def {
    const char* map;
    const ee_watch* watches;
    std::uint32_t count;
    std::uint32_t built_index;
};

constexpr ee_watch ee_blackscreen = {0, ee_kind::flag, "initial_blackscreen_passed", nullptr, nullptr};
constexpr ee_watch ee_transit[] = {
    ee_blackscreen,
    {1, ee_kind::equals, "scr_zm_ui_gametype", "zclassic", nullptr},
    {2, ee_kind::flag, "power_on", nullptr, nullptr},
    {3, ee_kind::field, "power_event_in_progress", nullptr, nullptr},
    {4, ee_kind::path, "sq_progress", "rich", "A_jetgun_built"},
    {5, ee_kind::path, "sq_progress", "rich", "A_jetgun_tower"},
    {7, ee_kind::path, "sq_progress", "rich", "FINISHED"},
    {8, ee_kind::path, "sq_progress", "maxis", "FINISHED"},
};
constexpr ee_watch ee_highrise[] = {
    ee_blackscreen,
    {1, ee_kind::flag, "sq_atd_drg_puzzle_complete", nullptr, nullptr},
    {2, ee_kind::flag, "sq_tower_active", nullptr, nullptr},
    {3, ee_kind::player_int, "num_perks", nullptr, nullptr},
    {4, ee_kind::player_has, "perk_purchased", nullptr, nullptr},
    {5, ee_kind::player_has, "laststand", nullptr, nullptr},
    {7, ee_kind::player_has, "revivetrigger", nullptr, nullptr},
};
constexpr ee_watch ee_prison[] = {
    ee_blackscreen,
    {1, ee_kind::flag, "dryer_cycle_active", nullptr, nullptr},
    {2, ee_kind::flag, "gondola_initialized", nullptr, nullptr},
    {3, ee_kind::flag, "gondola_in_motion", nullptr, nullptr},
    {4, ee_kind::flag, "plane_boarded", nullptr, nullptr},
    {5, ee_kind::flag, "nixie_ee_flashing", nullptr, nullptr},
    {7, ee_kind::length, "a_important_codes", nullptr, nullptr},
    {8, ee_kind::entity, "m_headphones", nullptr, nullptr},
    {9, ee_kind::defined, "winner", nullptr, nullptr},
};
constexpr ee_watch ee_buried[] = {
    ee_blackscreen,
    {2, ee_kind::flag, "chest_has_been_used", nullptr, nullptr},
    {3, ee_kind::flag, "spawn_ghosts", nullptr, nullptr},
    {4, ee_kind::flag, "sq_tpo_special_round_active", nullptr, nullptr},
    {5, ee_kind::flag, "sq_wisp_saved_with_time_bomb", nullptr, nullptr},
    {7, ee_kind::field, "buried_sq_maxis_complete", nullptr, nullptr},
    {8, ee_kind::field, "buried_sq_richtofen_complete", nullptr, nullptr},
    {9, ee_kind::field, "metagame_sq_maxis_complete", nullptr, nullptr},
    {10, ee_kind::field, "metagame_sq_richtofen_complete", nullptr, nullptr},
};
constexpr ee_watch ee_tomb[] = {
    ee_blackscreen,
    {1, ee_kind::flag, "activate_zone_nml", nullptr, nullptr},
    {2, ee_kind::field, "n_soul_boxes_completed", nullptr, nullptr},
    {3, ee_kind::field, "n_staffs_crafted", nullptr, nullptr},
    {4, ee_kind::flag, "ee_all_staffs_placed", nullptr, nullptr},
    {5, ee_kind::flag, "ee_mech_zombie_hole_opened", nullptr, nullptr},
    {7, ee_kind::flag, "ee_samantha_released", nullptr, nullptr},
    {8, ee_kind::field, "intermission", nullptr, nullptr},
};
constexpr ee_map_def ee_maps[] = {
    {"zm_transit", ee_transit, static_cast<std::uint32_t>(std::size(ee_transit)), 0},
    {"zm_highrise", ee_highrise, static_cast<std::uint32_t>(std::size(ee_highrise)), 0},
    {"zm_prison", ee_prison, static_cast<std::uint32_t>(std::size(ee_prison)), 0},
    {"zm_buried", ee_buried, static_cast<std::uint32_t>(std::size(ee_buried)), 11},
    {"zm_tomb", ee_tomb, static_cast<std::uint32_t>(std::size(ee_tomb)), 0},
};

struct ee_container {
    std::uint32_t obj = 0;
    bool strings = false;
    std::uint32_t head = 0;
    bool scanned = false;
    ULONGLONG rescan_at = 0;
    std::unordered_map<std::uint32_t, std::uint32_t> known;
};

struct ee_slot {
    bool bound = false;
    std::uint32_t child = 0, key = 0;
};

struct ee_state {
    std::uint32_t generation = 0xFFFFFFFF;
    std::uint32_t flag_array = 0;
    const ee_map_def* map = nullptr;
    ee_container level, flags, player;
    ee_slot slots[max_watches];
    std::uint32_t player_bounds[2] = {};
    std::unordered_map<std::uint32_t, std::string> names;
} ee;

ee_container* ee_owner(const ee_watch& watch) {
    switch (watch.kind) {
    case ee_kind::flag: return &ee.flags;
    case ee_kind::player_int:
    case ee_kind::player_has: return &ee.player;
    default: return &ee.level;
    }
}

bool ee_child_name(const ee_container& container, std::uint32_t key, std::string& out) {
    if (container.strings) {
        char buffer[64];
        if (key == 0 || key >= child_limit || !text(g.strings + static_cast<address_t>(key) * 24 + 4, buffer, sizeof(buffer))) return false;
        out = buffer;
        return true;
    }
    auto known = ee.names.find(key);
    if (known == ee.names.end()) {
        char buffer[64];
        if (!field_name(key, buffer, sizeof(buffer))) return false;
        known = ee.names.emplace(key, buffer).first;
    }
    out = known->second;
    return true;
}

void ee_bind(ee_container& container, std::uint32_t index, std::uint32_t key) {
    std::string name;
    if (!ee_child_name(container, key, name)) return;
    for (std::uint32_t i = 0; i < ee.map->count; ++i) {
        const ee_watch& watch = ee.map->watches[i];
        if (!ee.slots[i].bound && ee_owner(watch) == &container && name == watch.name) ee.slots[i] = {true, index, key};
    }
}

void ee_scan(ee_container& container) {
    if (!container.obj) return;
    if (!container.scanned && GetTickCount64() < container.rescan_at) return;
    std::uint32_t bounds[2] = {};
    if (!read(g.children + container.obj * 8, bounds, sizeof(bounds))) return;
    if (container.scanned && bounds[0] == container.head) return;
    const bool full = !container.scanned;
    if (full) container.known.clear();
    bool complete = true;
    std::uint32_t current = bounds[0], steps = 0;
    while (current != 0) {
        child c{};
        if (++steps > chain_limit || !read_child(current, c) || c.owner != container.obj) {
            complete = false;
            break;
        }
        auto seen = container.known.find(current);
        if (!full && seen != container.known.end() && seen->second == c.key) break;
        container.known[current] = c.key;
        ee_bind(container, current, c.key);
        current = c.next;
    }
    container.scanned = complete;
    if (complete) container.head = bounds[0];
    else container.rescan_at = GetTickCount64() + resolve_interval_ms;
}

bool ee_entry(ee_container& container, ee_slot& slot, child& out) {
    if (!slot.bound) return false;
    if (!read_child(slot.child, out) || out.owner != container.obj || out.key != slot.key) {
        slot.bound = false;
        container.scanned = false;
        container.rescan_at = GetTickCount64() + resolve_interval_ms;
        return false;
    }
    return true;
}

bool ee_array_child(std::uint32_t array, const char* key, child& out) {
    std::uint32_t type = 0, bounds[2] = {};
    if (!object_type(array, type) || type != type_array || !read(g.children + array * 8, bounds, sizeof(bounds))) return false;
    std::uint32_t current = bounds[0], steps = 0;
    while (current != 0 && ++steps <= 64) {
        child c{};
        char name[64];
        if (!read_child(current, c) || c.owner != array) return false;
        if (c.key < child_limit && text(g.strings + static_cast<address_t>(c.key) * 24 + 4, name, sizeof(name)) && std::strcmp(name, key) == 0) {
            out = c;
            return true;
        }
        current = c.next;
    }
    return false;
}

std::int32_t ee_value(const ee_watch& watch, ee_slot& slot) {
    child c{};
    if (!ee_entry(*ee_owner(watch), slot, c)) return 0;
    std::uint32_t type = 0;
    switch (watch.kind) {
    case ee_kind::flag:
    case ee_kind::field:
    case ee_kind::player_int: return c.tag == tag_int ? static_cast<std::int32_t>(c.payload) : 0;
    case ee_kind::defined:
    case ee_kind::player_has: return 1;
    case ee_kind::length: {
        std::uint16_t count = 0;
        if (c.tag != tag_object || !object_type(c.payload, type) || type != type_array || !read(g.objects + static_cast<address_t>(c.payload) * 8 + 2, &count, sizeof(count))) return 0;
        return count;
    }
    case ee_kind::entity:
        if (c.tag != tag_object || !object_type(c.payload, type)) return 0;
        return type == type_entity ? 1 : 2;
    case ee_kind::equals: {
        char buffer[64];
        return c.tag == tag_string && c.payload != 0 && c.payload < child_limit && text(g.strings + static_cast<address_t>(c.payload) * 24 + 4, buffer, sizeof(buffer)) && std::strcmp(buffer, watch.key1) == 0 ? 1 : 0;
    }
    case ee_kind::path: {
        child first{}, second{};
        if (c.tag != tag_object || !ee_array_child(c.payload, watch.key1, first) || first.tag != tag_object || !ee_array_child(first.payload, watch.key2, second) || second.tag != tag_int) return 0;
        return static_cast<std::int32_t>(second.payload);
    }
    }
    return 0;
}

void ee_track_player() {
    std::uint32_t bounds[2] = {};
    if (!g.players_array || !read(g.children + g.players_array * 8, bounds, sizeof(bounds))) return;
    if (ee.player.obj && bounds[0] == ee.player_bounds[0] && bounds[1] == ee.player_bounds[1]) return;
    ee.player_bounds[0] = bounds[0];
    ee.player_bounds[1] = bounds[1];
    std::uint32_t entity = 0, current = bounds[0], steps = 0;
    while (current != 0 && ++steps <= 64) {
        child c{};
        if (!read_child(current, c) || c.owner != g.players_array) return;
        if (c.key == 0x800000 && c.tag == tag_object) entity = c.payload;
        current = c.next;
    }
    if (entity == ee.player.obj) return;
    ee.player = ee_container{};
    ee.player.obj = entity;
    for (std::uint32_t i = 0; i < ee.map->count; ++i)
        if (ee_owner(ee.map->watches[i]) == &ee.player) ee.slots[i] = ee_slot{};
}

void read_ee(t6r_sample& out) {
    if (ee.generation != g.generation || ee.flag_array != g.flag_array) {
        ee = ee_state{};
        ee.generation = g.generation;
        ee.flag_array = g.flag_array;
        for (const ee_map_def& candidate : ee_maps)
            if (std::strcmp(g.map, candidate.map) == 0) ee.map = &candidate;
        ee.level.obj = g.level;
        ee.flags.obj = g.flag_array;
        ee.flags.strings = true;
    }
    if (!ee.map) return;
    ee_track_player();
    ee_scan(ee.level);
    ee_scan(ee.flags);
    ee_scan(ee.player);
    for (std::uint32_t i = 0; i < ee.map->count; ++i) {
        const ee_watch& watch = ee.map->watches[i];
        const std::int32_t value = ee_value(watch, ee.slots[i]);
        out.values[watch.index] = value;
        out.watch_known |= 1u << watch.index;
        if (value != 0) out.watch_set |= 1u << watch.index;
    }
    if (ee.map->built_index) read_set(out, ee.map->built_index);
}

void read_watches(t6r_sample& out) {
    const profile_def& profile = profiles[g.profile];
    for (std::uint32_t i = 0; i < profile.count; ++i) {
        watch_slot& slot = g.watches[i];
        if (!slot.known) continue;
        child c{};
        if (!read_child(slot.child, c) || c.owner != slot.owner || c.key != slot.key || c.tag != tag_int) {
            slot.known = false;
            g.complete = false;
            g.next_resolve = 0;
            continue;
        }
        out.watch_known |= 1u << i;
        out.values[i] = static_cast<std::int32_t>(c.payload);
        if (c.payload != 0) out.watch_set |= 1u << i;
    }
}

}

T6R_API int t6r_api_version(void) { return T6R_API_VERSION; }

T6R_API void t6r_poll(uint32_t profile, t6r_sample* out) {
    if (!out) return;
    std::memset(out, 0, sizeof(*out));
    if (profile >= sizeof(profiles) / sizeof(profiles[0])) return;

    if (g.process && WaitForSingleObject(g.process, 0) != WAIT_TIMEOUT) close_process();
    if (!g.process) attach();
    if (!g.process) {
        out->status = g.unsupported ? T6R_UNSUPPORTED : T6R_NO_PROCESS;
        return;
    }
    if (g.profile != profile) {
        g.profile = profile;
        forget_level();
    }

    for (int attempt = 0; attempt < sample_attempts; ++attempt) {
        out->status = T6R_NO_LEVEL;
        out->watch_known = out->watch_set = 0;
        std::memset(out->values, 0, sizeof(out->values));
        std::uint32_t clock = 0, level = 0, clock_after = 0, time = 0, time_after = 0;
        if (!u32(g.build->vm_clock, clock) || !u32(g.build->level_root, level)) return;
        if (level != g.level || clock < g.last_clock) {
            g.level = level;
            ++g.generation;
            forget_level();
        }
        g.last_clock = clock;
        out->clock = clock;
        const bool timed = g.build->engine_time && u32(g.build->engine_time, time);
        out->generation = g.generation;
        out->process_id = GetProcessId(g.process);
        out->level_id = level;
        if (level == 0 || level >= object_limit) return;

        if (!(g.complete && g.full) && GetTickCount64() >= g.next_resolve) {
            resolve(level);
            if (g.complete && select_player_fields()) {
                resolve_players();
                g.complete = g.players_ok;
            }
        }
        read_watches(*out);
        if (g.complete && select_player_fields()) track_players(*out);
        if (g.complete && profiles[g.profile].built_set) read_set(*out, built_value);
        if (g.complete && profiles[g.profile].ee) read_ee(*out);
        if (!u32(g.build->vm_clock, clock_after)) continue;
        if (timed && (!u32(g.build->engine_time, time_after) || time_after != time)) {
            ++out->retries;
            continue;
        }
        if (clock_after == clock) {
            if (timed) out->clock = frame_tick(clock, time);
            out->status = g.complete ? T6R_READY : T6R_RESOLVING;
            std::memcpy(out->map, g.map, sizeof(out->map));
            return;
        }
        ++out->retries;
    }
    out->status = T6R_RESOLVING;
    out->watch_known = out->watch_set = 0;
    std::memset(out->values, 0, sizeof(out->values));
}

T6R_API void t6r_close(void) { close_process(); }
