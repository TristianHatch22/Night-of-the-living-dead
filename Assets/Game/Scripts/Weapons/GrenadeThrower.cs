using System.Collections;
using UnityEngine;

namespace NOTLD
{
    /// <summary>Lives on the player. Holds the grenade count and throws on G / D-pad up.</summary>
    public class GrenadeThrower : MonoBehaviour
    {
        public static GrenadeThrower I;
        public const int StartCount = 2;
        public const int MaxCount = 4;

        public int Count = StartCount;
        public bool Throwing { get; private set; }

        PlayerController player;
        CharacterController cc;
        int lastRound = 1;

        void Awake()
        {
            I = this;
            player = GetComponent<PlayerController>();
            cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            if (GameManager.I != null) { lastRound = Mathf.Max(1, GameManager.I.Round); GameManager.I.OnRoundChanged += OnRound; }
        }

        void OnDestroy()
        {
            if (GameManager.I != null) GameManager.I.OnRoundChanged -= OnRound;
            if (I == this) I = null;
        }

        void OnRound(int r)
        {
            if (r > lastRound) Count = Mathf.Min(MaxCount, Count + 1);   // +1 grenade every new round
            lastRound = Mathf.Max(lastRound, r);
        }

        /// <summary>Refill (e.g. after a revive).</summary>
        public void Restock(int n) { Count = Mathf.Min(MaxCount, Mathf.Max(Count, n)); }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Paused || gm.GameOver || player == null) return;
            if (player.IsDown || player.IsDead || player.IsDrinking || Throwing) return;
            if (!GameInput.GrenadePressed()) return;
            if (Count <= 0) { SFX.Play("deny", 0.5f); return; }
            StartCoroutine(ThrowRoutine());
        }

        IEnumerator ThrowRoutine()
        {
            Throwing = true;
            Count--;
            SFX.Play("grenade_pin", 0.7f, Random.Range(0.97f, 1.03f));
            GameInput.Rumble(0.1f, 0.2f, 0.08f);
            yield return new WaitForSeconds(0.22f);

            if (player != null && !player.IsDown && player.Cam != null)
            {
                var cam = player.Cam.transform;
                Vector3 origin = cam.position + cam.forward * 0.5f + cam.right * 0.18f - cam.up * 0.12f;
                if (Physics.Linecast(cam.position, origin, ~0, QueryTriggerInteraction.Ignore)) origin = cam.position + cam.forward * 0.1f;
                Vector3 vel = cam.forward * 15f + Vector3.up * 3.2f + (cc != null ? cc.velocity * 0.6f : Vector3.zero);
                Grenade.Create(origin, vel, cc);
                SFX.Play("grenade_throw", 0.7f, Random.Range(0.95f, 1.05f));
                player.AddRecoil(0.8f, 0f);
                GameInput.Rumble(0.2f, 0.4f, 0.1f);
            }
            yield return new WaitForSeconds(0.25f);
            Throwing = false;
        }
    }
}
