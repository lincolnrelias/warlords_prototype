using Unity.Entities;

namespace Data
{
// Data component to hold faction health totals
    public struct FactionHealthData : IComponentData
    {
        public float friendlyCurrentHealth;
        public float friendlyMaxHealth;
        public float hostileCurrentHealth;
        public float hostileMaxHealth;
    
        public float FriendlyHealthPercentage => friendlyMaxHealth > 0 ? friendlyCurrentHealth / friendlyMaxHealth : 0;
        public float HostileHealthPercentage => hostileMaxHealth > 0 ? hostileCurrentHealth / hostileMaxHealth : 0;
    }
}