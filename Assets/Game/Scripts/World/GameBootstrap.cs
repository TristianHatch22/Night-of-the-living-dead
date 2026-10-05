using UnityEngine;

namespace NOTLD
{
    /// <summary>Put on one object in the Game scene. Spawns the player, managers and HUD and applies settings.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Time.timeScale = 1f;
            GameSettings.Load();
            SFX.Prewarm();
            if (FindFirstObjectByType<GameManager>() == null) new GameObject("GameManager").AddComponent<GameManager>();

            var spawn = GameObject.Find("PlayerSpawn");
            var pgo = new GameObject("Player");
            Vector3 basePos = spawn ? spawn.transform.position : Vector3.zero;
            if (NetSession.IsOnline) basePos += (spawn ? spawn.transform.right : Vector3.right) * ((NetSession.LocalIndex - 1.5f) * 1.1f);
            pgo.transform.position = basePos;
            pgo.transform.rotation = spawn ? spawn.transform.rotation : Quaternion.identity;
            pgo.AddComponent<CharacterController>();
            pgo.AddComponent<PlayerController>();

            new GameObject("HUD").AddComponent<HUD>();
        }

        void Start()
        {
            GameSettings.ApplyAll();
        }
    }
}
