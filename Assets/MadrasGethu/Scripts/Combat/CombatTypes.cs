using System;
using UnityEngine;

namespace MadrasGethu
{
    public enum Team { Player, Enemy }

    /// <summary>Poses the placeholder body (or an Animator, via a trigger with the same name) can play.</summary>
    public enum FighterPose
    {
        Idle, Guard, Block,
        Jab, Cross, Hook, BodyBlow,
        Haymaker, Uppercut, Backfist, Kick, Stomp,
        Grab, Knee, Throw, Slam,
        Counter, Dodge,
        Flinch, Knockdown, GetUp, GetUpFront, Dead,
        Windup, Taunt,
        Jump, FlyingKick, DodgeLeft, DodgeRight, DodgeBack, Roll,
        Headbutt, Push, Parry, StaggerBack,
        // Enemy-only moves
        RowdySwing, RowdyKick, RowdyShove, RowdyGrab, BearHug, Charge, GroundPound, Roar,
        // Weapons and objects (hero and enemies)
        PickUp, WeaponSwing, WeaponSmash, ThrowObject,
        Beg,
        // Hero combo specials and wrestling finishers
        ShoulderBarge, LegSweep, Clothesline, BackElbow,
        Lift, Suplex, Spear, ElbowDrop,
        // Grab strike combos
        FrontKick, Roundhouse, PushKick, FlyingKnee,
        // Street moves: free strikes, the tackle and the rowdy's reactions to them
        SpinKick, SupermanPunch, LowKick, Tackle, GroundPunch, Kneel, GutHit
    }

    /// <summary>What kind of threat an enemy attack is, which decides how the hero must answer it.</summary>
    public enum AttackKind
    {
        Strike,        // red: parry or block
        Unblockable,   // orange: dodge
        Grab,          // yellow: dodge, or break free once caught
        Charge,        // orange: running shoulder charge, dodge sideways
        GroundPound,   // orange: area slam around the attacker, get out of range
    }

    public enum TelegraphColor { None, Red, Orange, Yellow }

    public enum HitResult { Ignored, Hit, Blocked }

    /// <summary>Everything a target needs to know about an incoming strike.</summary>
    public struct HitInfo
    {
        public Fighter attacker;
        public float damage;
        public Vector3 direction;   // horizontal direction from attacker to target
        public float knockback;     // horizontal push speed
        public float launch;        // upward speed; anything > 0 knocks the target down
        public bool heavy;          // breaks guard-less armour, causes bigger reactions
        public bool unblockable;
        public bool hitsDowned;     // can hit enemies lying on the ground (stomps, finishers)
        public bool dazes;          // leaves the target standing but dazed (open to grabs and finishers)
        public FighterPose pose;    // the blow that landed (Idle = unknown): picks the hit reaction and the sound
        public Sfx sound;           // impact sound (Auto = from the blow, Silent = the move plays its own)
    }

    /// <summary>One move in a combo string. Times are in seconds.</summary>
    [Serializable]
    public class AttackData
    {
        public string name;
        public FighterPose pose;
        public float damage = 10f;
        public float range = 1.6f;
        public float arc = 90f;           // degrees in front of the attacker that the hit covers
        public float startup = 0.12f;     // wind-up before the hit lands
        public float recovery = 0.25f;    // time after the hit before you can move freely
        public float lunge = 1.2f;        // how far the attacker steps in
        public float knockback = 2f;
        public float launch = 0f;
        public float gethuGain = 6f;
        public bool heavy;
        public bool unblockable;
        public bool hitsDowned;
        public bool announce;             // show the move's name on screen (specials and finishers)
        public bool dazes;                // hero: a clean hit leaves him dazed
        public AttackKind kind;           // enemies: how the hero has to respond
        public bool feint;                // enemies: fake attack that punishes an early parry
        public float weight = 1f;         // enemies: how often this move is picked
        public float hitStop = 0.05f;
        public float shake = 0.1f;
        // Weapon moves
        public FighterPose reaction = FighterPose.Idle;   // the hit reaction to use (Idle = from the pose)
        public float spin;                // degrees the attacker turns through during the windup (Silambam spin)
        public bool crouch;               // down on one knee for it (brick drop)
        public bool breakWeapon;          // the weapon breaks on him (plank smash, chair smash)
        [NonSerialized] public AttackData next;                 // follow-up that starts by itself (thrust -> overhead)
        [NonSerialized] public Action<EnemyFighter> onHit;      // extra effect on the first rowdy it lands on

        public AttackData(string name, FighterPose pose, float damage)
        {
            this.name = name;
            this.pose = pose;
            this.damage = damage;
        }

        public TelegraphColor Telegraph =>
            kind == AttackKind.Grab ? TelegraphColor.Yellow
            : kind == AttackKind.Strike && !unblockable ? TelegraphColor.Red
            : TelegraphColor.Orange;

        public AttackData Clone() => (AttackData)MemberwiseClone();

        public HitInfo ToHit(Fighter attacker, Vector3 direction, float damageMultiplier = 1f)
        {
            return new HitInfo
            {
                attacker = attacker,
                damage = damage * damageMultiplier,
                direction = direction,
                knockback = knockback,
                launch = launch,
                heavy = heavy,
                unblockable = unblockable,
                hitsDowned = hitsDowned,
                dazes = dazes,
                pose = reaction != FighterPose.Idle ? reaction : pose,
            };
        }
    }
}
