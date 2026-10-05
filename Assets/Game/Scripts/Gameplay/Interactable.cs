using System.Collections.Generic;
using UnityEngine;

namespace NOTLD
{

    public abstract class Interactable : MonoBehaviour
    {
        public float Range = 2.2f;
        public virtual bool HoldToUse => false;
        /// <summary>Prompt text ({USE} is replaced with the current use-button label), or null when nothing to do.</summary>
        public abstract string Prompt(PlayerController p);
        public abstract void Interact(PlayerController p);
        public virtual Vector3 Point => transform.position + Vector3.up;
        /// <summary>Price of the current action (0 = free / none). Used for the "not enough points" hint.</summary>
        public virtual int PriceFor(PlayerController p) => 0;
    }
}
