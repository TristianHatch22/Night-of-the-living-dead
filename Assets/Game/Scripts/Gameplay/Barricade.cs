using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{
// ===================================================================== BARRICADE WINDOW
    public class Barricade : Interactable
    {
        public const int MaxBoards = 6;
        public int Boards = MaxBoards;
        public bool ClimbLocked;
        public Transform OutsidePoint, InsidePoint, SillPoint;
        public List<GameObject> BoardObjects = new List<GameObject>();
        float nextRepair;

        public override bool HoldToUse => true;
        public override Vector3 Point => SillPoint ? SillPoint.position : transform.position;

        void Awake()
        {
            if (OutsidePoint == null) OutsidePoint = transform.Find("OutsidePoint");
            if (InsidePoint == null) InsidePoint = transform.Find("InsidePoint");
            if (SillPoint == null) SillPoint = transform.Find("SillPoint");
            if (BoardObjects.Count == 0)
            {
                var b = transform.Find("Boards");
                if (b) foreach (Transform c in b) BoardObjects.Add(c.gameObject);
            }
            Range = 2.0f;
            Refresh();
        }

        public void TearBoard()
        {
            if (Boards <= 0) return;
            Boards--;
            var go = BoardObjects[Boards];
            SFX.PlayAt("board", go.transform.position, 0.8f, Random.Range(0.8f, 1.1f));
            Effects.Puff(go.transform.position, 0.2f, 0.25f);
            Refresh();
            NetGame.I?.SendGameState(ulong.MaxValue);
        }

        /// <summary>Client side: the host says the window now has n boards.</summary>
        public void SetBoards(int n)
        {
            n = Mathf.Clamp(n, 0, MaxBoards);
            if (n == Boards) return;
            if (n < Boards)
            {
                var go = BoardObjects[Boards - 1];
                SFX.PlayAt("board", go.transform.position, 0.8f, Random.Range(0.8f, 1.1f));
                Effects.Puff(go.transform.position, 0.2f, 0.25f);
            }
            else SFX.PlayAt("board", BoardObjects[n - 1].transform.position, 0.7f, 1.3f);
            Boards = n;
            Refresh();
        }

        public void RepairBoard()
        {
            if (Boards >= MaxBoards) return;
            Boards++;
            SFX.PlayAt("board", BoardObjects[Boards - 1].transform.position, 0.7f, 1.3f);
            Refresh();
            NetGame.I?.SendGameState(ulong.MaxValue);
        }

        void Refresh()
        {
            for (int i = 0; i < BoardObjects.Count; i++) BoardObjects[i].SetActive(i < Boards);
        }

        public override string Prompt(PlayerController p) => Boards < MaxBoards ? "Hold {USE} to rebuild barrier" : null;

        public override void Interact(PlayerController p)
        {
            if (Boards >= MaxBoards || Time.time < nextRepair) return;
            if (NetSession.IsClient) { nextRepair = Time.time + 0.7f; NetGame.SendRepair(); return; } // the host rebuilds it and pays us
            // don't rebuild while a zombie is climbing through
            if (ClimbLocked) return;
            nextRepair = Time.time + 0.7f;
            RepairBoard();
            GameManager.I.AddRepairPoints(10);
        }
    }
}
