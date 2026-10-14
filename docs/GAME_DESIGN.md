# Madras Gethu: Game Design

*"Gethu"* is Madras Bashai slang for swagger and style.

## Pitch
A third-person open-district brawler, like *Sleeping Dogs* and *Yakuza*, set in North Chennai.
The world is gritty, in the vein of *Vada Chennai*, and the fights have mass-hero moments (finishers, slow-mo, hero-intro beats).

## Story hook
**Maaran** was a rising boxer in the North Chennai boxing-clan tradition (the world of *Sarpatta Parambarai*) until a fixed fight ended his career.
Years later he comes back to Royapuram. A smuggling syndicate run from Kasimedu harbour, protected by local political rowdies, now controls his old clan's ground.
He may be working undercover for the police (the Sleeping Dogs angle), which divides his loyalties between the clan, the cops and the street.

## The district (one dense area, Yakuza-style)
| Area | Flavour | Gameplay |
|---|---|---|
| **Kasimedu fishing harbour** | Boats, fish auctions, ice factories, night smuggling | Gang fights, chases on the docks *(prototype arena)* |
| **Royapuram / Washermanpet** | Narrow lanes, boxing clubs, chawl-style houses | Home base, boxing gym, training |
| **Parry's Corner / Burma Bazaar** | Dense markets, grey-market electronics | Shops, side quests, crowd brawls |
| **Marina Beach** | Bajji stalls, the lighthouse, night arena | Street fights, a date mini-game |
| **T. Nagar, Ranganathan Street** | Crowded shopping streets | Foot chases |
| **Kodambakkam** | Film studios, fan clubs | Comedy side stories, cut-out banner missions |

## Combat styles (unlocked over the story)
1. **North Chennai boxing**: jabs, hooks, footwork and counters. *(Starting style, in the prototype.)*
2. **Silambam**: Tamil bamboo-staff fighting with wide crowd-control sweeps. Poles, pipes and umbrellas from the world can be used.
3. **Adimurai / Varma Kalai**: strikes to pressure points that stun, slow or paralyse. The technical style.
4. **Malyutham**: Tamil wrestling, for grabs, throws and slams.

You switch styles mid-fight, as in Yakuza 0.

## Core systems
- **Gethu meter** (heat): stylish play fills it. It is spent on finishers or Gethu Mode. *(Prototype.)*
- **Environmental finishers**: tea kadai, fish crates, autos *(prototype)*, then shutters, Pongal pot stacks, temple-tank railings, film-poster hoardings, ice-factory blocks.
- **Counters and dodges** on red-flash telegraphs. *(Prototype.)*
- **Group AI**: rowdies take turns attacking. *(Prototype.)*
- **Enemy archetypes**: rowdy, Thadiyan (bruiser) and boss *(prototype)*, then a knife rowdy, a lathi (stick) rowdy, a bike gang and a Silambam master.
- **Wrestling finishers with QTE prompts** (the start of Malyutham): body slam, dhobi pachad, German suplex, powerbomb, spear, elbow drop, spinebuster. *(Prototype.)*
- **Combo grades**: Nalla, Semma, Mass, Thalaivaa, multiplying Gethu. *(Prototype.)*

## Skill tree (planned, Spider-Man style)
Earn XP from fights and missions, spend skill points in three branches. Every move already has an ID in `MoveUnlocks`, so a skill only needs to call `MoveUnlocks.Unlock(...)`. Everything is unlocked in the prototype.

| Branch | Example skills |
|---|---|
| **Kuthu (strikes)** | Charged Haymaker, Shoulder Barge chain, Air Hammer, Back Elbow, longer combo window |
| **Malyutham (grapples)** | Dhobi Pachad, German Suplex, Powerbomb, Spinebuster, faster break-free, wider QTE timing |
| **Theru (street)** | Leg Sweep, Shoulder Rush, Spear, Clothesline, Wall Bounce, weapon durability, cheaper finishers |

## Side content (later)
- **Gaana karaoke** (the Yakuza karaoke equivalent)
- Gully cricket, kabaddi, carrom
- **Street food heals you**: filter coffee, kothu parotta, Marina sundal, Burma atho
- Auto-rickshaw driving and races
- Fan-club banner missions in Kodambakkam
- Boxing-club tournament ladder

## Audio and language
- Tamil and Madras Bashai dialogue with English subtitles
- Gaana and kuthu tracks for big fights, with a hero theme on finishers

## Roadmap
| Milestone | Content |
|---|---|
| **M1: Combat prototype** ✅ | Controller, camera, boxing combos, block/counter/dodge, grabs, environmental finishers, Gethu, 3 enemy types, waves |
| M1.5: Combat depth ✅ | Collar-grab IK, two-timing parry, enemy-only moves, weapons, combo specials, wrestling finishers with QTE, button prompts |
| M2: Real characters | Mixamo characters and animations, hit sounds, gaana BGM, bloom and post-processing, a proper uGUI HUD |
| M3: Second style | Silambam with a staff weapon and weapon pickups; switching styles |
| M4: Explorable street | A Royapuram lane with NPC crowds, shops, a food stall that heals, and story triggers |
| M5: First mission | Cutscene, a chase, a boss at Kasimedu, plus save/load |
| M6: Side activities | Gaana karaoke, carrom, more of the district |
| M7: Progression | Skill tree, XP, gamepad button icons |
