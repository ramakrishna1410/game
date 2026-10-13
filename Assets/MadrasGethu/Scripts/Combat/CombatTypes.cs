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
        Flinch, Knockdown, GetUp, Dead,
        Windup, Taunt
    }

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
        public float hitStop = 0.05f;
        public float shake = 0.1f;

        public AttackData(string name, FighterPose pose, float damage)
        {
            this.name = name;
            this.pose = pose;
            this.damage = damage;
        }

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
            };
        }
    }
}
