using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// What the rowdies can read off the hero (see EnemyTactics.cs):
    ///   - the strings he keeps repeating (the same one three times in a row and they block it),
    ///   - how long he's been standing there holding someone (a friend comes for him after 1.5 s),
    ///   - whether he's in a finisher (they close in and wait for it to end).
    /// </summary>
    public partial class PlayerFighter
    {
        /// <summary>Holding a rowdy this long gives his friends the go-ahead to come for you.</summary>
        public const float HoldGrace = 1.5f;
        /// <summary>A repeated string stays "read" this long after you last threw it.</summary>
        const float ReadMemory = 6f;

        readonly List<string> stringMoves = new List<string>();
        string lastString;
        int stringRepeats;
        float lastStringTime = -10f, lastStringMove = -10f;
        bool readAnnounced;

        /// <summary>The string being thrown now is the one you've repeated: rowdies see it coming.</summary>
        public bool StringIsRead { get; private set; }

        /// <summary>This blow is part of the read string (heavies and unblockables still get through).</summary>
        public bool CurrentHitIsRead => StringIsRead && State == PlayerState.Attack && currentAttack != null
                                        && !currentAttack.heavy && !currentAttack.unblockable;

        /// <summary>Seconds you've been holding someone (0 when you aren't).</summary>
        public float GrabHoldTime => State == PlayerState.Grab ? stateTimer : 0f;

        /// <summary>In a finisher, grab combo, mount or weapon finisher: can't be touched, so they wait for it to end.</summary>
        public bool IsBusyFinishing =>
            State == PlayerState.Wrestle || State == PlayerState.Finisher || State == PlayerState.Combo ||
            State == PlayerState.Mount || State == PlayerState.HeadClash || (State == PlayerState.WeaponMove && weaponFinisherActive);

        /// <summary>First block of a read string: true once, so the "reading you" popup shows once.</summary>
        public bool AnnounceRead()
        {
            if (readAnnounced) return false;
            readAnnounced = true;
            return true;
        }

        /// <summary>StartAttack hook: adds the move to the current string (a gap of over a second starts a new one).</summary>
        void TrackString(AttackData a)
        {
            if (a == null || a == counter || a == counterUppercut || a == counterThrow || a == grabMiss) return;
            if (Time.time - lastStringMove > 1.0f) CommitString();
            stringMoves.Add(a.name);
            lastStringMove = Time.time;
            StringIsRead = stringRepeats >= 2 && stringMoves.Count >= 2 && Time.time - lastStringTime < ReadMemory && MatchesLastString();
        }

        void CommitString()
        {
            if (stringMoves.Count >= 2)
            {
                string sig = string.Join(">", stringMoves);
                if (sig == lastString && Time.time - lastStringTime < ReadMemory) stringRepeats++;
                else { lastString = sig; stringRepeats = 1; readAnnounced = false; }
                lastStringTime = Time.time;
            }
            stringMoves.Clear();
            StringIsRead = false;
        }

        bool MatchesLastString()
        {
            if (lastString == null) return false;
            var parts = lastString.Split('>');
            if (stringMoves.Count > parts.Length) return false;
            for (int i = 0; i < stringMoves.Count; i++)
                if (parts[i] != stringMoves[i]) return false;
            return true;
        }

        /// <summary>A fresh rowdy can shove your grab off. Returns true if he did (you stumble back).</summary>
        bool GrabShrugged(EnemyFighter target, bool behind)
        {
            if (!target.TryShrugOffGrab(this, behind)) return false;
            Visual.Play(FighterPose.Flinch, 0.35f);
            Motor.AddLunge(-transform.forward, 0.6f);
            SetState(PlayerState.Stagger);
            stateTimer = 0.05f;
            return true;
        }

        /// <summary>
        /// Holding someone and a friend of his is sneaking up behind you: LMB lets go and elbows him.
        /// Returns true if it happened.
        /// </summary>
        bool TryHoldElbow()
        {
            var sneak = EnemyFighter.Flanker;
            if (sneak == null || !sneak.SneakWarning || sneak == grabbed) return false;
            if (Flat(sneak.transform.position - transform.position).magnitude > 3.2f) return false;
            Prompts.Add(new PromptItem { key = "LMB", label = "Let go + elbow" });
            if (!GameInput.LightPressed) return false;
            ReleaseGrab(transform.forward * 1.5f, false);
            StartAttack(backElbow, sneak);
            return true;
        }
    }
}
