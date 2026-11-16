#pragma once
#include <stdint.h>

#ifdef T6R_BUILD
#define T6R_API extern "C" __declspec(dllexport)
#else
#define T6R_API extern "C" __declspec(dllimport)
#endif

#define T6R_MAX_WATCHES 16

enum t6r_status {
    T6R_NO_PROCESS = 0,
    T6R_UNSUPPORTED = 1,
    T6R_NO_LEVEL = 2,
    T6R_RESOLVING = 3,
    T6R_READY = 4,
};

// v[0] initial_blackscreen_passed (easter egg: supported maps only), zm_prison v[6] afterlife corpse mask
enum t6r_profile {
    T6R_PROFILE_SONGS = 0,        // 1 meteor_counter 2 snd115count 3 found_ee_radio_count 4 music_override 5 nixie_puzzle_solved 7 mannequin_count 8 round_number 9 activate_zone_nml
    T6R_PROFILE_ROUNDS = 1,       // 1 round_number 2 round_start_time
    T6R_PROFILE_PACK_A_PUNCH = 2, // 1-4 players[i].pap_used 5 players 7 round_number 8 power_on 9 zclassic 10 town 11 all_zones_captured|spawn_ghosts 12 dryer_cycle_active 13 n_plane_pieces_found 14 plane_built
    T6R_PROFILE_BUILDABLES = 3,   // 1 built
    T6R_PROFILE_RAINBOW = 4,      // 1 num_perks 2 player_perk_purchase_limit 3 has_upgraded_shovel 5 players
    T6R_PROFILE_EASTER_EGG = 5,
};
// built
//   transit  turbine riotshield turret electric_trap powerswitch jetgun pap cattlecatcher hatch busladder sq_common
//   highrise springpad slipgun sq_common
//   buried   turbine headchopper subwoofer sq_common springpad tower oillamp tpo_switch
//   prison   shield plane packasplat
//   tomb     drone shield staff_fire staff_air staff_lightning staff_water
// easter egg (all maps 15 players)
//   transit  1 zclassic 2 power_on 3 power_event_in_progress 4 A_jetgun_built 5 A_jetgun_tower 7 rich FINISHED 8 maxis FINISHED
//   highrise 1 sq_atd_drg_puzzle_complete 2 sq_tower_active 3 num_perks 4 perk_purchased 5 laststand 7 revivetrigger 8 orb
//   prison   1 dryer_cycle_active 2 gondola_initialized 3 gondola_in_motion 4 plane_boarded 5 nixie_ee_flashing 7 a_important_codes 8 m_headphones 9 winner
//   buried   2 chest_has_been_used 3 spawn_ghosts 4 sq_tpo_special_round_active 5 sq_wisp_saved_with_time_bomb 7 sq maxis 8 sq richtofen 9 metagame maxis 10 metagame richtofen 11 built 12 sq_is_max_tower_built 13 sq_ip_puzzle_complete 14 sq_ml_curr_lever
//   tomb     1 activate_zone_nml 2 _challenges.s_team.n_completed 3 n_staffs_crafted 4 ee_all_staffs_placed 5 ee_mech_zombie_hole_opened 7 ee_samantha_released 8 intermission

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
    uint32_t process_id;
    uint32_t level_id;
    uint32_t clock_dips;
    uint32_t velocity_known;
    float velocity[3];
    float frame_ms;
    uint32_t offhost;
} t6r_sample;
#pragma pack(pop)

T6R_API void t6r_poll(uint32_t profile, t6r_sample* out);
T6R_API void t6r_close(void);
