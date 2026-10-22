using UnityEngine;

namespace CampaignDemo
{
    // Changes simulation time; each unit keeps its own speed and terrain rules.
    public sealed class BattleTimeControl : MonoBehaviour
    {
        public float Multiplier { get; private set; } = 1;
        float previousTimeScale;

        void OnEnable()
        {
            previousTimeScale = Time.timeScale;
            SetMultiplier(1);
        }

        public void SetMultiplier(float multiplier)
        {
            if (!isActiveAndEnabled || LocalPvpSession.Active != null && !LocalPvpSession.Active.IsHost) return;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return;
            Multiplier = Mathf.Clamp(multiplier, 1, 4);
            Time.timeScale = Multiplier;
        }

        public void ApplyNetworkMultiplier(float multiplier)
        {
            if (LocalPvpSession.Active == null || LocalPvpSession.Active.IsHost || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return;
            Multiplier = Mathf.Clamp(multiplier, 1, 4);
            Time.timeScale = Multiplier;
        }

        void OnDisable()
        {
            Time.timeScale = previousTimeScale;
            Multiplier = 1;
        }
    }
}
