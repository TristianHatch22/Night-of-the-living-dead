using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
// ===================================================================== PERK SLUSHY MACHINE
    public enum Perk { Juggernog, DoubleTap, QuickRevive, SpeedCola }

    public class PerkMachine : Interactable
    {
        public Perk Perk;
        public int Cost;
        public Transform[] Paddles;

        public static string PerkName(Perk p) => p switch
        {
            Perk.Juggernog => "Juggernog",
            Perk.DoubleTap => "Double Tap Root Beer",
            Perk.QuickRevive => "Quick Revive",
            _ => "Speed Cola",
        };
        public static string PerkShort(Perk p) => p switch
        {
            Perk.Juggernog => "JUG",
            Perk.DoubleTap => "DT",
            Perk.QuickRevive => "QR",
            _ => "SC",
        };
        public static int PerkCost(Perk p) => p switch
        {
            Perk.Juggernog => 2500,
            Perk.DoubleTap => 2000,
            Perk.QuickRevive => 500, // solo price
            _ => 3000,
        };

        void Awake()
        {
            Cost = PerkCost(Perk);
            var list = new List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>()) if (t.name == "Paddle") list.Add(t);
            Paddles = list.ToArray();
        }

        void Update()
        {
            if (Paddles == null) return;
            for (int i = 0; i < Paddles.Length; i++) Paddles[i].Rotate(0, (i % 2 == 0 ? 90f : -110f) * Time.deltaTime, 0, Space.Self);
        }

        public override int PriceFor(PlayerController p) => p.HasPerk(Perk) ? 0 : Cost;

        public override string Prompt(PlayerController p)
        {
            if (p.HasPerk(Perk)) return null;
            if (p.Perks.Count >= 4) return "You can only hold 4 perks";
            if (Perk == Perk.QuickRevive && p.SelfRevivesUsed >= 3) return "Quick Revive is no longer available";
            return $"Press {{USE}} for a {PerkName(Perk)} slushy  [Cost: {Cost}]";
        }

        public override void Interact(PlayerController p)
        {
            if (p.HasPerk(Perk) || p.IsDrinking || p.Perks.Count >= 4) return;
            if (Perk == Perk.QuickRevive && p.SelfRevivesUsed >= 3) return;
            if (!GameManager.I.Spend(Cost)) { SFX.Play("deny"); return; }
            SFX.Play("buy");
            SFX.PlayAt("jingle_" + Perk, transform.position + Vector3.up, 1f);
            p.DrinkPerk(Perk);
        }
    }
}
