using System;
using UnityEngine;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Players;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Lightweight person-like placeholder model for the remote opponent.
    /// Communicates "this is a person" using basic geometric primitives (Torso, Head, Limbs, Guild Emblem)
    /// without requiring heavy rigged character art or ML body reconstruction.
    /// Rendered ONLY for the remote player; never instantiated or shown for the local player.
    /// </summary>
    public class RemotePlayerVisual : MonoBehaviour
    {
        public string OwnerPlayerId { get; set; }

        private MeshRenderer _bodyRenderer;
        private MeshRenderer _headRenderer;
        private MeshRenderer _emblemRenderer;
        private Material _visualMaterial;
        private Material _emblemMaterial;

        private void Awake()
        {
            BuildPlaceholderModel();
        }

        public void BuildPlaceholderModel()
        {
            // If already built or part of an existing PlayerCharacter hierarchy, do not recreate
            if (transform.Find("Visual_Model") != null || transform.Find("CharacterView") != null || GetComponentInParent<PlayerCharacter>() != null)
            {
                _bodyRenderer = GetComponentInChildren<MeshRenderer>();
                return;
            }

            // Check if player model prefab is available from Resources or Assets
            var modelPrefab = Resources.Load<GameObject>("PlayerCharacter_Prefab");
#if UNITY_EDITOR
            if (modelPrefab == null)
            {
                modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCharacter_Prefab.prefab");
            }
#endif
            if (modelPrefab != null)
            {
                var instance = Instantiate(modelPrefab, transform);
                instance.name = "Visual_Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                var cols = instance.GetComponentsInChildren<Collider>();
                foreach (var c in cols) StripCollider(c.gameObject);
                var pc = instance.GetComponent<PlayerCharacter>();
                if (pc != null)
                {
                    if (Application.isPlaying) Destroy(pc);
                    else DestroyImmediate(pc);
                }

                _bodyRenderer = instance.GetComponentInChildren<MeshRenderer>();
                return;
            }

            Debug.LogWarning("[RemotePlayerVisual] Authoritative PlayerCharacter_Prefab not found. Visual model delayed until prefab load.");
        }

        public void ApplyGuildVisual(GuildData guild)
        {
            Color guildColor = GetGuildColor(guild);
            if (_visualMaterial != null)
            {
                if (_visualMaterial.HasProperty("_BaseColor"))
                    _visualMaterial.SetColor("_BaseColor", guildColor);
                if (_visualMaterial.HasProperty("_Color"))
                    _visualMaterial.color = guildColor;
            }
            if (_bodyRenderer != null)
            {
                if (!Application.isPlaying)
                {
                    var block = new MaterialPropertyBlock();
                    _bodyRenderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", guildColor);
                    block.SetColor("_Color", guildColor);
                    _bodyRenderer.SetPropertyBlock(block);
                }
                else if (_bodyRenderer.material != null)
                {
                    if (_bodyRenderer.material.HasProperty("_BaseColor"))
                        _bodyRenderer.material.SetColor("_BaseColor", guildColor);
                    if (_bodyRenderer.material.HasProperty("_Color"))
                        _bodyRenderer.material.color = guildColor;
                }
            }
            if (_emblemMaterial != null)
            {
                _emblemMaterial.color = guildColor * 1.3f;
            }
        }

        private Color GetGuildColor(GuildData guild)
        {
            if (guild == null) return new Color(0.3f, 0.6f, 0.9f);
            string id = guild.guildId?.ToUpperInvariant() ?? "";
            if (id.Contains("MAGMA")) return new Color(0.85f, 0.3f, 0.15f);
            if (id.Contains("AQUA")) return new Color(0.15f, 0.55f, 0.9f);
            if (id.Contains("PLASMA")) return new Color(0.65f, 0.25f, 0.85f);
            if (id.Contains("HOLY") || id.Contains("TRINITY")) return new Color(0.9f, 0.8f, 0.2f);
            if (id.Contains("GAMBIT")) return new Color(0.2f, 0.7f, 0.45f);
            return new Color(0.3f, 0.6f, 0.9f);
        }

        private void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        private void Update()
        {
            // Billboard the guild emblem toward main camera
            if (_emblemRenderer != null && Camera.main != null)
            {
                _emblemRenderer.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
