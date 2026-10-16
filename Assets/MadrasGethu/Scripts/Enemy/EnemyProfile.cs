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

        // Enemy-only behaviour
        public List<AttackData[]> combos = new List<AttackData[]>();   // chained strings (boss)
        public float comboChance;
        public float feintChance;
        public bool throwsObjects;          // waiting rowdies lob bottles / bricks
        public bool picksUpWeapons;         // walks over to a lathi / pipe on the street
        public bool countersBlocks;         // blocks a flurry and hits back (boss)
        public bool hasPhaseTwo;            // boss rage at half health
        public string grabOutcome = "headbutt";   // headbutt, bearhug, throw
        public float grabEscape;            // chance to shove off a grab when fresh (never when dazed / staggered / from behind)
        public float flankChance;           // chance to circle behind the hero before attacking

        public AttackData PickAttack()
        {
            float total = 0f;
            foreach (var a in attacks) total += a.weight;
            float r = Random.value * total;
            foreach (var a in attacks)
            {
                r -= a.weight;
                if (r <= 0f) return a;
            }
            return attacks[attacks.Count - 1];
        }

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

        /// <summary>Untrained street thug: wild swings, dirty kicks, shoves, grabs and thrown bottles.</summary>
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
                throwsObjects = true,
                picksUpWeapons = true,
                grabOutcome = "headbutt",
                grabEscape = 0.25f,
                flankChance = 0.35f,
            };
            p.attacks.Add(new AttackData("Wild Swing", FighterPose.RowdySwing, 9f)
                { weight = 3.5f, startup = 0.55f, recovery = 0.6f, range = 1.6f, lunge = 1.3f, knockback = 3f });
            p.attacks.Add(new AttackData("Stomach Kick", FighterPose.RowdyKick, 10f)
                { weight = 2.5f, startup = 0.6f, recovery = 0.6f, range = 1.8f, lunge = 1.2f, knockback = 4f });
            p.attacks.Add(new AttackData("Shove", FighterPose.RowdyShove, 4f)
                { weight = 2f, startup = 0.5f, recovery = 0.5f, range = 1.4f, lunge = 1.2f, knockback = 7f });
            p.attacks.Add(new AttackData("Grab", FighterPose.RowdyGrab, 0f)
                { weight = 2f, kind = AttackKind.Grab, startup = 0.6f, recovery = 0.6f, range = 1.4f, lunge = 1.4f });
            return p;
        }

        /// <summary>Slow heavy bruiser: shoulder charge, ground pound, bear hug. Doesn't flinch from light hits.</summary>
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
                grabOutcome = "bearhug",
                grabEscape = 0.25f,
                flankChance = 0.15f,
            };
            p.attacks.Add(new AttackData("Haymaker", FighterPose.RowdySwing, 18f)
                { weight = 2f, startup = 0.75f, recovery = 0.7f, range = 1.9f, lunge = 1.5f, knockback = 6f, heavy = true });
            p.attacks.Add(new AttackData("Ground Pound", FighterPose.GroundPound, 20f)
                { weight = 1.5f, kind = AttackKind.GroundPound, startup = 0.95f, recovery = 1.0f, range = 2.8f, arc = 360f,
                  lunge = 0.5f, knockback = 6f, launch = 4f, heavy = true, unblockable = true });
            p.attacks.Add(new AttackData("Shoulder Charge", FighterPose.Charge, 22f)
                { weight = 1.5f, kind = AttackKind.Charge, startup = 0.75f, recovery = 0.8f, range = 6f,
                  knockback = 10f, launch = 4f, heavy = true, unblockable = true });
            p.attacks.Add(new AttackData("Bear Hug", FighterPose.BearHug, 0f)
                { weight = 1.2f, kind = AttackKind.Grab, startup = 0.75f, recovery = 0.8f, range = 1.6f, lunge = 1.5f });
            return p;
        }

        /// <summary>Experienced fighter: combos, feints, counters a flurry, grabs and throws; rages at half health.</summary>
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
                attackCooldown = new Vector2(1.6f, 2.8f),
                gethuReward = 40f,
                shirt = new Color(0.95f, 0.95f, 0.95f),          // spotless white politician shirt
                pants = new Color(0.95f, 0.95f, 0.95f),
                accent = new Color(0.1f, 0.5f, 0.2f),            // party-colour veshti border
                comboChance = 0.55f,
                feintChance = 0.2f,
                countersBlocks = true,
                hasPhaseTwo = true,
                grabOutcome = "throw",
                grabEscape = 0.5f,
                flankChance = 0.25f,
            };
            var jab = new AttackData("Jab", FighterPose.Jab, 10f)
                { startup = 0.42f, recovery = 0.4f, range = 1.6f, lunge = 1.4f, knockback = 3f };
            var hook = new AttackData("Hook", FighterPose.Hook, 15f)
                { startup = 0.55f, recovery = 0.55f, range = 1.7f, lunge = 1.4f, knockback = 5f, arc = 140f, heavy = true };
            var rushKick = new AttackData("Rush Kick", FighterPose.Kick, 20f)
                { startup = 0.75f, recovery = 0.8f, range = 2.4f, lunge = 3f, knockback = 7f, launch = 4f,
                  heavy = true, unblockable = true };
            var grab = new AttackData("Grab Throw", FighterPose.RowdyGrab, 0f)
                { kind = AttackKind.Grab, startup = 0.55f, recovery = 0.7f, range = 1.5f, lunge = 1.5f };
            p.attacks.Add(jab);
            p.attacks.Add(hook);
            p.attacks.Add(rushKick);
            p.attacks.Add(grab);
            p.combos.Add(new[] { jab, jab, hook });
            p.combos.Add(new[] { jab, hook, rushKick });
            p.combos.Add(new[] { hook, jab });
            return p;
        }
    }
}
