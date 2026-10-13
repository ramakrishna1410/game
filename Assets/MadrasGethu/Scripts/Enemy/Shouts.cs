using UnityEngine;

namespace MadrasGethu
{
    public enum ShoutType { Attack, Hurt, Grab, Throw, Taunt, Beg, Flee, BossRage, Feint, PickUp }

    /// <summary>
    /// Madras Bashai lines that pop up over enemies (voice acting can replace these later).
    /// </summary>
    public static class Shouts
    {
        static readonly string[] AttackLines = { "Dei!", "Vaada!", "Adi da avana!", "Podu!", "Vidaadhe!", "Saavu da!" };
        static readonly string[] HurtLines = { "Aiyyo!", "Amma!", "Aah!", "Dei, valikkudhu!" };
        static readonly string[] GrabLines = { "Pudi da!", "Maatikitta!", "Odaadhe!" };
        static readonly string[] ThrowLines = { "Indha!", "Pudichiko!", "Vaangu!" };
        static readonly string[] TauntLines = { "Vaa da, vaa!", "Unakku dhairiyam irundha vaa!", "Kasimedu-la vandhu scene-aa?", "Enna paakura?" };
        static readonly string[] BegLines = { "Anna, vittudunga anna!", "Thappu pannitten anna!", "Adikkaadheenga, please!" };
        static readonly string[] FleeLines = { "Odu da!", "Aala vidu!", "Ayyo, thappichikko!" };
        static readonly string[] BossRageLines = { "Kasimedu enakku sondham!", "Dei! Ellaarum vaanga!", "Ini un kadhai mudinjidhu!" };
        static readonly string[] FeintLines = { "Haha! Bayandhutiyaa?", "Ennada, jumpa?" };
        static readonly string[] PickUpLines = { "Idhu pothum!", "Ippo paaru!" };

        public static void Say(Fighter who, ShoutType type, float chance = 1f)
        {
            if (who == null || GameManager.Instance == null || Random.value > chance) return;
            string[] lines;
            switch (type)
            {
                case ShoutType.Attack: lines = AttackLines; break;
                case ShoutType.Hurt: lines = HurtLines; break;
                case ShoutType.Grab: lines = GrabLines; break;
                case ShoutType.Throw: lines = ThrowLines; break;
                case ShoutType.Taunt: lines = TauntLines; break;
                case ShoutType.Beg: lines = BegLines; break;
                case ShoutType.Flee: lines = FleeLines; break;
                case ShoutType.BossRage: lines = BossRageLines; break;
                case ShoutType.Feint: lines = FeintLines; break;
                default: lines = PickUpLines; break;
            }
            string line = lines[Random.Range(0, lines.Length)];
            GameManager.Instance.AddPopup(line, who.Center + Vector3.up * 1.3f, new Color(1f, 1f, 1f, 0.95f), 0.65f);
        }
    }
}
