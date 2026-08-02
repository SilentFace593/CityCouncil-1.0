using Colossal.Entities;
using Game.Policies;
using Unity.Entities;

namespace CityCouncil
{
    public static class CouncilPolicyUtils
    {
        /// <summary>
        /// Vérifie si une politique donnée (par son entité prefab) est active sur l'entité
        /// cible (typiquement un district). Même logique que EVSubsidyPolicyUtils.IsPolicyActive.
        /// </summary>
        public static bool IsPolicyActive(EntityManager em, Entity target, Entity policyPrefabEntity)
        {
            if (policyPrefabEntity == Entity.Null) return false;
            if (!em.TryGetBuffer<Policy>(target, true, out var buffer)) return false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].m_Policy == policyPrefabEntity)
                    return (buffer[i].m_Flags & PolicyFlags.Active) != 0;
            }
            return false;
        }
    }
}
