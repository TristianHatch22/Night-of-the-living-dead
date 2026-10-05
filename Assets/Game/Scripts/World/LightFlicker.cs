using UnityEngine;

namespace NOTLD
{
    public class LightFlicker : MonoBehaviour
    {
        Light l; float baseI, next;
        void Awake() { l = GetComponent<Light>(); baseI = l.intensity; }
        void Update()
        {
            if (Time.time > next)
            {
                l.intensity = Random.value < 0.06f ? baseI * Random.Range(0.1f, 0.4f) : baseI * Random.Range(0.92f, 1.05f);
                next = Time.time + Random.Range(0.03f, 0.15f);
            }
        }
    }
}
