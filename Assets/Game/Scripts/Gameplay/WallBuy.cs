using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
// ===================================================================== WALL BUY
    public class WallBuy : Interactable
    {
        public string WeaponName;
        public int Price;
        public override Vector3 Point => transform.position;
        WeaponDef Def => WeaponDatabase.Get(WeaponName);

        void Awake() { Range = 2.4f; }

        public override int PriceFor(PlayerController p)
        {
            var def = Def; if (def == null) return 0;
            var slot = p.Weapons.FindSlot(def);
            if (slot == null) return Price;
            return (slot.Mag >= def.Mag && slot.Reserve >= def.Reserve) ? 0 : Price / 2;
        }

        public override string Prompt(PlayerController p)
        {
            var def = Def; if (def == null) return null;
            var slot = p.Weapons.FindSlot(def);
            if (slot == null) return $"Press {{USE}} to buy {def.Name}  [Cost: {Price}]";
            if (slot.Mag >= def.Mag && slot.Reserve >= def.Reserve) return null;
            return $"Press {{USE}} to buy {def.Name} ammo  [Cost: {Price / 2}]";
        }

        public override void Interact(PlayerController p)
        {
            var def = Def; if (def == null) return;
            var slot = p.Weapons.FindSlot(def);
            int cost = slot == null ? Price : Price / 2;
            if (slot != null && slot.Mag >= def.Mag && slot.Reserve >= def.Reserve) return;
            if (!GameManager.I.Spend(cost)) { SFX.Play("deny"); return; }
            SFX.Play("buy");
            if (slot == null) p.Weapons.GiveWeapon(def); else p.Weapons.RefillAmmo(slot);
        }
    }
}
