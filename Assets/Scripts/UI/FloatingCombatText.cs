using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace ArGuildEncounter.UI
{
    /// <summary>
    /// FLOATING_COMBAT_TEXT
    /// Displays floating feedback text in world/screen space with upward drift and alpha fade.
    /// Used for -Damage, +Heal, MISS, CRIT, STATUS, SKIP, FALLBACK, and DEFEATED.
    /// </summary>
    public class FloatingCombatText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _textMesh;
        [SerializeField] private float _driftSpeed = 1.0f;
        [SerializeField] private float _lifetime = 1.4f;

        private Color _initialColor;

        private void Awake()
        {
            if (_textMesh == null)
            {
                _textMesh = GetComponent<TextMeshPro>();
                if (_textMesh == null)
                {
                    _textMesh = gameObject.AddComponent<TextMeshPro>();
                }
            }
            _textMesh.fontSize = 4;
            _textMesh.alignment = TextAlignmentOptions.Center;
        }

        public void Spawn(string message, Vector3 worldPosition, Color textColor, float scale = 1.0f)
        {
            transform.position = worldPosition + Vector3.up * 0.5f;
            transform.localScale = Vector3.one * scale;
            _textMesh.text = message;
            _textMesh.color = textColor;
            _initialColor = textColor;

            // Orient towards camera
            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }

            StartCoroutine(AnimateAndDestroy());
        }

        private IEnumerator AnimateAndDestroy()
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _lifetime;

                // Drift upward
                transform.position = startPos + Vector3.up * (_driftSpeed * elapsed);

                // Face camera continuously
                if (Camera.main != null)
                {
                    transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
                }

                // Fade alpha in last 40% of lifetime
                if (t > 0.6f)
                {
                    float alpha = Mathf.Lerp(1.0f, 0f, (t - 0.6f) / 0.4f);
                    _textMesh.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, alpha);
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
