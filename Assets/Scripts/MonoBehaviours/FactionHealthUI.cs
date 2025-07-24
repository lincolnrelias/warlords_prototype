using Data;
using Unity.Entities;
using UnityEngine;

namespace MonoBehaviours
{
    public class FactionHealthUI : MonoBehaviour
    {
        [Header("UI References")]
        public UnityEngine.UI.Image friendlyHealthBar;
        public UnityEngine.UI.Image hostileHealthBar;
    
        [Header("Optional Text Displays")]
        public TMPro.TextMeshProUGUI friendlyHealthText;
        public TMPro.TextMeshProUGUI hostileHealthText;

        private void Update()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world?.EntityManager == null) return;

            var query = world.EntityManager.CreateEntityQuery(typeof(FactionHealthData));
            if (query.CalculateEntityCount() == 0) return;

            var healthData = query.GetSingleton<FactionHealthData>();

            // Update health bars
            if (friendlyHealthBar != null)
                friendlyHealthBar.fillAmount = healthData.FriendlyHealthPercentage;
            
            if (hostileHealthBar != null)
                hostileHealthBar.fillAmount = healthData.HostileHealthPercentage;

            // Update text displays (optional)
            if (friendlyHealthText != null)
                friendlyHealthText.text = $"{healthData.friendlyCurrentHealth:F0}/{healthData.friendlyMaxHealth:F0}";

            if (hostileHealthText != null)
                hostileHealthText.text = $"{healthData.hostileCurrentHealth:F0}/{healthData.hostileMaxHealth:F0}";
        }
    }

}