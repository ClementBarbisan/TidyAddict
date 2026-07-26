using System;
using UnityEngine;

namespace Projectiles
{
    public class PlayerSpellVFX : MonoBehaviour
    {
        [Serializable]
        private struct SpellVFXEntry
        {
            public SpellType Type;
            public ParticleSystem Particle;
        }

        [SerializeField] private SpellVFXEntry[] _entries;

        public void Play(SpellType type)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type == type)
                {
                    if (entry.Particle != null)
                        entry.Particle.Play();
                    return;
                }
            }

            Debug.LogWarning($"[PlayerSpellVFX] Aucun ParticleSystem assigné pour {type}");
        }
    }
}