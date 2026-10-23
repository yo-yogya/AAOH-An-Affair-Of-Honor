using System;
using System.Collections;
using UnityEngine;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.UI
{
    /// <summary>
    /// COMBAT_VFX_MANAGER
    /// Spawns visual responses for logical combat events:
    /// Projectiles, Hit Flashes, Miss puffs, Heal auras, Crit bursts, and Floating Combat Text.
    /// Fully decoupled from combat math: driven purely by AttackResolutionResult events.
    /// </summary>
    public class CombatVFXManager : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float _projectileSpeed = 12.0f;

        [Header("Colors by Element")]
        [SerializeField] private Color _fireColor = new Color(1.0f, 0.4f, 0.1f);
        [SerializeField] private Color _waterColor = new Color(0.2f, 0.6f, 1.0f);
        [SerializeField] private Color _iceColor = new Color(0.6f, 0.9f, 1.0f);
        [SerializeField] private Color _lightningColor = new Color(1.0f, 0.9f, 0.2f);
        [SerializeField] private Color _windColor = new Color(0.7f, 1.0f, 0.8f);
        [SerializeField] private Color _groundColor = new Color(0.7f, 0.5f, 0.3f);
        [SerializeField] private Color _grassColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color _lightColor = new Color(1.0f, 1.0f, 0.8f);
        [SerializeField] private Color _darkColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] private Color _nullColor = new Color(0.8f, 0.8f, 0.8f);

        public void PlayAttackSequence(
            Vector3 casterPos,
            Vector3 targetPos,
            SpellData spell,
            AttackResolutionResult result,
            Action onImpactCallback = null)
        {
            StartCoroutine(ExecuteAttackSequenceCoroutine(casterPos, targetPos, spell, result, onImpactCallback));
        }

        private IEnumerator ExecuteAttackSequenceCoroutine(
            Vector3 startPos,
            Vector3 endPos,
            SpellData spell,
            AttackResolutionResult result,
            Action onImpactCallback)
        {
            Color elementColor = GetElementColor(spell != null ? spell.type1 : CombatElement.Null);

            // 1. Spawn Projectile
            GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proj.name = $"Projectile_{spell?.spellId ?? "Spell"}";
            proj.transform.localScale = Vector3.one * 0.25f;
            proj.transform.position = startPos + Vector3.up * 1.0f;

            var col = proj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = proj.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = elementColor;
                rend.material = mat;
            }

            Vector3 targetCenter = endPos + Vector3.up * 1.0f;
            float journeyTime = Vector3.Distance(proj.transform.position, targetCenter) / _projectileSpeed;
            float elapsed = 0f;
            Vector3 startProjPos = proj.transform.position;

            while (elapsed < journeyTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / journeyTime);
                proj.transform.position = Vector3.Lerp(startProjPos, targetCenter, t);
                yield return null;
            }

            Destroy(proj);

            // 2. Impact Event
            onImpactCallback?.Invoke();

            if (!result.isHit)
            {
                SpawnFloatingText("MISS", targetCenter, Color.gray, 1.2f);
                SpawnImpactBurst(targetCenter, Color.gray, 0.3f);
            }
            else
            {
                if (result.isCritical)
                {
                    SpawnFloatingText("CRIT!", targetCenter + Vector3.up * 0.4f, Color.yellow, 1.5f);
                }

                SpawnFloatingText($"-{result.finalDamageDealt}", targetCenter, elementColor, 1.3f);
                SpawnImpactBurst(targetCenter, elementColor, 0.6f);

                if (result.healingApplied > 0)
                {
                    SpawnFloatingText($"+{result.healingApplied} HP", startPos + Vector3.up * 1.2f, Color.green, 1.2f);
                }

                if (result.didSelfDamageTrigger)
                {
                    SpawnFloatingText($"-{result.selfDamageDealt} RECOIL", startPos + Vector3.up * 0.8f, Color.red, 1.1f);
                }

                if (result.didBackfireTrigger)
                {
                    SpawnFloatingText($"-{result.backfireDamageDealt} BACKFIRE!", startPos + Vector3.up * 0.8f, new Color(1f, 0.2f, 0.5f), 1.3f);
                }

                if (result.didMoveSkipApply)
                {
                    SpawnFloatingText("MOVE SKIP!", targetCenter + Vector3.up * 0.8f, Color.magenta, 1.3f);
                }

                if (result.didForceFallbackApply)
                {
                    SpawnFloatingText("FORCE FALLBACK!", targetCenter + Vector3.up * 0.8f, Color.cyan, 1.3f);
                }
            }
        }

        public void SpawnFloatingText(string message, Vector3 worldPosition, Color color, float scale = 1.0f)
        {
            GameObject textGo = new GameObject("FloatingTextInstance");
            var fct = textGo.AddComponent<FloatingCombatText>();
            fct.Spawn(message, worldPosition, color, scale);
        }

        private void SpawnImpactBurst(Vector3 position, Color color, float size)
        {
            GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "ImpactBurst";
            burst.transform.position = position;
            burst.transform.localScale = Vector3.one * (size * 0.5f);

            var col = burst.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = burst.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = color;
                rend.material = mat;
            }

            StartCoroutine(AnimateBurst(burst, size));
        }

        private IEnumerator AnimateBurst(GameObject burst, float targetSize)
        {
            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 startScale = burst.transform.localScale;
            Vector3 targetScale = Vector3.one * targetSize;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (burst != null)
                {
                    burst.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                }
                yield return null;
            }

            if (burst != null)
            {
                Destroy(burst);
            }
        }

        private Color GetElementColor(CombatElement element)
        {
            switch (element)
            {
                case CombatElement.Fire: return _fireColor;
                case CombatElement.Water: return _waterColor;
                case CombatElement.Ice: return _iceColor;
                case CombatElement.Lightning: return _lightningColor;
                case CombatElement.Wind: return _windColor;
                case CombatElement.Ground: return _groundColor;
                case CombatElement.Grass: return _grassColor;
                case CombatElement.Light: return _lightColor;
                case CombatElement.Dark: return _darkColor;
                default: return _nullColor;
            }
        }
    }
}
