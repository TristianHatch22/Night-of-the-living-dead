using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
// ===================================================================== CARDBOARD MYSTERY BOX
    public class MysteryBox : Interactable
    {
        enum State { Idle, Rolling, Ready }
        public const int Cost = 950;
        State state;
        Transform lidL, lidR, display;
        GameObject current;
        WeaponDef result;
        float timer, nextSwap, nextRattle;

        void Awake()
        {
            lidL = transform.Find("FlapL");
            lidR = transform.Find("FlapR");
            display = new GameObject("Display").transform;
            display.SetParent(transform, false);
            display.localPosition = new Vector3(0, 0.75f, 0);
            Range = 2.3f;
        }

        public override int PriceFor(PlayerController p) => state == State.Idle ? Cost : 0;

        public override string Prompt(PlayerController p)
        {
            switch (state)
            {
                case State.Idle: return $"Press {{USE}} to open the Mystery Box  [Cost: {Cost}]";
                case State.Ready: return $"Press {{USE}} to take {result.Name}";
            }
            return null;
        }

        public override void Interact(PlayerController p)
        {
            if (state == State.Idle)
            {
                if (!GameManager.I.Spend(Cost)) { SFX.Play("deny"); return; }
                SFX.PlayAt("box_open", transform.position, 1f);
                state = State.Rolling;
                timer = 0; nextSwap = 0; nextRattle = 0.25f;
                result = WeaponDatabase.RandomBoxWeapon(p.Weapons.OwnedNames());
            }
            else if (state == State.Ready)
            {
                p.Weapons.GiveWeapon(result);
                SFX.Play("buy");
                Close(false);
            }
        }

        void Update()
        {
            float target = state == State.Idle ? 0f : 1f;
            if (lidL) lidL.localRotation = Quaternion.Slerp(lidL.localRotation, Quaternion.Euler(0, 0, 130f * target), Time.deltaTime * 6f);
            if (lidR) lidR.localRotation = Quaternion.Slerp(lidR.localRotation, Quaternion.Euler(0, 0, -130f * target), Time.deltaTime * 6f);

            if (state == State.Rolling)
            {
                timer += Time.deltaTime;
                display.localPosition = new Vector3(0, Mathf.Lerp(0.3f, 0.95f, Mathf.Clamp01(timer / 1.5f)), 0);
                if (timer >= nextSwap)
                {
                    ShowModel(timer > 4.5f ? result : WeaponDatabase.All[Random.Range(1, WeaponDatabase.All.Count)]);
                    nextSwap = timer + Mathf.Lerp(0.07f, 0.35f, timer / 4.5f);
                }
                // rattling glass inside the cardboard box, with the occasional squeaky toy
                if (timer >= nextRattle)
                {
                    SFX.PlayAt(Random.value < 0.15f ? "box_squeak" : "box_rattle", transform.position + Vector3.up * 0.5f, 0.8f, Random.Range(0.85f, 1.2f));
                    nextRattle = timer + Random.Range(0.22f, 0.4f);
                }
                if (timer > 4.5f)
                {
                    ShowModel(result); state = State.Ready; timer = 0;
                    SFX.PlayAt("box_reveal", transform.position, 1f);
                }
            }
            else if (state == State.Ready)
            {
                timer += Time.deltaTime;
                display.localPosition = new Vector3(0, Mathf.Lerp(0.95f, 0.35f, timer / 12f), 0);
                if (timer > 12f) Close(true);
            }
            display.Rotate(0, 40f * Time.deltaTime, 0);
        }

        void ShowModel(WeaponDef def)
        {
            if (current) Destroy(current);
            var info = ViewmodelBuilder.Build(def, display, false);
            current = info.Root;
            current.transform.localScale = Vector3.one * 1.6f;
            current.transform.localRotation = Quaternion.Euler(0, 90, 0);
        }

        void Close(bool timedOut)
        {
            state = State.Idle;
            if (current) Destroy(current);
            current = null;
            SFX.PlayAt(timedOut ? "box_womp" : "box_close", transform.position, 1f);
        }
    }
}
