using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    public enum EnemyKind { Rowdy, Thadiyan, Boss }

    /// <summary>Stats and moves for each enemy type. Tweak numbers here to tune difficulty.</summary>
    public class EnemyProfile
    {
        public string displayName;
        public float health;
        public float runSpeed;
        public float circleSpeed;
        public float scale = 1f;
        public int poise;               // light hits absorbed before flinching (0 = always flinches)
        public float blockChance;       // chance to block a light attack while circling
        public bool grabbable = true;   // heavies can only be grabbed while staggered
        public Vector2 attackCooldown;  // random seconds between attacks
        public float gethuReward;
        public Color shirt, pants, accent;
        public List<AttackData> attacks = new List<AttackData>();

        public static EnemyProfile Get(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Thadiyan: return Thadiyan();
                case EnemyKind.Boss: return Boss();
                default: return Rowdy();
            }
        }

        static readonly string[] RowdyNames = { "Kasimedu Rowdy", "Harbour Goonda", "Settu's Boy", "Dock Rowdy", "Auto Stand Rowdy" };
        static readonly Color[] RowdyShirts =
        {
            new Color(0.75f, 0.2f, 0.18f), new Color(0.2f, 0.45f, 0.7f), new Color(0.85f, 0.65f, 0.15f),
            new Color(0.3f, 0.55f, 0.3f), new Color(0.55f, 0.25f, 0.55f),
        };

        static EnemyProfile Rowdy()
        {
            var p = new EnemyProfile
            {
                displayName = RowdyNames[Random.Range(0, RowdyNames.Length)],
                health = 60f,
                runSpeed = 4.2f,
                circleSpeed = 1.2f,
                poise = 0,
                blockChance = 0.1f,
                attackCooldown = new Vector2(2.2f, 4.0f),
                gethuReward = 8f,
                shirt = RowdyShirts[Random.Range(0, RowdyShirts.Length)],
                pants = new Color(0.9f, 0.88f, 0.82f),           // white veshti / lungi
                accent = new Color(0.25f, 0.15f, 0.1f),
            };
            p.attacks.Add(new AttackData("Punch", FighterPose.Cross, 8f)
                { startup = 0.5f, recovery = 0.5f, range = 1.5f, lunge = 1.2f, knockback = 3f });
            p.attacks.Add(new AttackData("Kick", FighterPose.Kick, 10f)
                { startup = 0.6f, recovery = 0.6f, range = 1.8f, lunge = 1.2f, knockback = 4f });
            return p;
        }

        static EnemyProfile Thadiyan()
        {
            var p = new EnemyProfile
            {
                displayName = "Thadiyan",
                health = 150f,
                runSpeed = 3.4f,
                circleSpeed = 0.9f,
                scale = 1.25f,
                poise = 3,
                blockChance = 0.25f,
                grabbable = false,
                attackCooldown = new Vector2(2.8f, 4.5f),
                gethuReward = 20f,
                shirt = new Color(0.12f, 0.12f, 0.14f),
                pants = new Color(0.35f, 0.25f, 0.5f),
                accent = new Color(0.9f, 0.75f, 0.2f),          // gold chain-belt
            };
            p.attacks.Add(new AttackData("Haymaker", FighterPose.Haymaker, 18f)
                { startup = 0.75f, recovery = 0.7f, range = 1.9f, lunge = 1.5f, knockback = 6f, heavy = true });
            p.attacks.Add(new AttackData("Overhead Slam", FighterPose.Slam, 24f)
                { startup = 0.95f, recovery = 0.9f, range = 2.1f, lunge = 1.0f, knockback = 5f, launch = 5f,
                  heavy = true, unblockable = true, arc = 120f });
            return p;
        }

        static EnemyProfile Boss()
        {
            var p = new EnemyProfile
            {
                displayName = "Kumaran Anna",
                health = 320f,
                runSpeed = 4.6f,
                circleSpeed = 1.4f,
                scale = 1.12f,
                poise = 4,
                blockChance = 0.4f,
                grabbable = false,
                attackCooldown = new Vector2(1.4f, 2.6f),
                gethuReward = 40f,
                shirt = new Color(0.95f, 0.95f, 0.95f),          // spotless white politician shirt
                pants = new Color(0.95f, 0.95f, 0.95f),
                accent = new Color(0.1f, 0.5f, 0.2f),            // party-colour veshti border
            };
            p.attacks.Add(new AttackData("Jab", FighterPose.Jab, 10f)
                { startup = 0.45f, recovery = 0.4f, range = 1.6f, lunge = 1.4f, knockback = 3f });
            p.attacks.Add(new AttackData("Hook", FighterPose.Hook, 16f)
                { startup = 0.6f, recovery = 0.55f, range = 1.7f, lunge = 1.4f, knockback = 5f, arc = 140f, heavy = true });
            p.attacks.Add(new AttackData("Rush Kick", FighterPose.Kick, 22f)
                { startup = 0.8f, recovery = 0.8f, range = 2.4f, lunge = 3f, knockback = 7f, launch = 4f,
                  heavy = true, unblockable = true });
            return p;
        }
    }
}
