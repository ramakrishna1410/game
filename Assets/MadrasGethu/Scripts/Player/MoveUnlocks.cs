using System;
using System.Collections.Generic;

namespace MadrasGethu
{
    /// <summary>Hero moves that a future skill tree (Spider-Man style) can lock and unlock.</summary>
    public enum MoveId
    {
        // Combo specials
        ShoulderBarge, AirHammer, ChargedHaymaker, LegSweep, ShoulderRush, Clothesline, BackElbow, WallBounce,
        // Wrestling finishers
        BodySlam, SpinDrop, GermanSuplex, Powerbomb, Spear, ElbowDrop, Spinebuster,
        // Grab strike combos
        RoyapuramRush, MachineGunKuthu, KasimeduKickStorm, ThalaivarSpecial, Jallikattu, GethuMassCombo,
        // Street moves (no grab)
        SpinningHeelKick, SupermanPunch, KaalOdaippu, RugbyTackle,
        // Holding him
        MuttaMothal, ArmTwist, EarClap, TripStomp, BowlingThrow,
        // Finishers only
        GiantSwing, ChokeOut, ThaiClinch, KadalThrow, IceBoxSlam,
        // Counters
        KaiOdaippu, CounterThrow, CounterFinisher, WeaponSteal, GrabReversal, EscapeStrike, Matador, DoubleCounter,
        // Weapons
        SilambamSutru, SilambamFinish, GolfSwing, PipeChoke, PlankBreak, BottleFinisher, BrickDrop, ChainWrap, TyreTrap, WeaponBlock,
    }

    /// <summary>
    /// Which moves the hero knows. Everything is unlocked for now. The skill tree will call
    /// Lock/Unlock and save the set; gameplay code only ever asks Has().
    /// </summary>
    public static class MoveUnlocks
    {
        static readonly HashSet<MoveId> unlocked = new HashSet<MoveId>((MoveId[])Enum.GetValues(typeof(MoveId)));

        public static event Action<MoveId> Unlocked;

        public static bool Has(MoveId move) => unlocked.Contains(move);

        public static void Unlock(MoveId move)
        {
            if (unlocked.Add(move) && Unlocked != null) Unlocked(move);
        }

        public static void Lock(MoveId move) => unlocked.Remove(move);

        public static void LockAll() => unlocked.Clear();
    }
}
