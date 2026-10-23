using System;
using UnityEngine;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.AR;

namespace ArGuildEncounter.Combat
{
    /// <summary>
    /// Decoupled player entity in the two-player PvP architecture.
    /// Distinguishes local player from remote opponent, managing identity,
    /// combat state, spatial tracking proxy, targeting hitbox, and visual representation.
    /// </summary>
    public class PlayerEntity
    {
        public string PlayerId { get; }
        public bool IsLocal { get; set; }
        public bool IsHost { get; set; }
        public GuildData Guild { get; set; }
        public PlayerCombatState CombatState { get; set; }
        public SpatialTrack SpatialTrack { get; set; }
        public PlayerHitbox Hitbox { get; set; }
        public RemotePlayerVisual Visual { get; set; }

        public PlayerEntity(string playerId, bool isLocal, bool isHost, GuildData guild = null)
        {
            PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            IsLocal = isLocal;
            IsHost = isHost;
            Guild = guild;
            SpatialTrack = new SpatialTrack(playerId);
        }

        public void BindHitbox(PlayerHitbox hitbox)
        {
            Hitbox = hitbox;
            if (Hitbox != null)
            {
                Hitbox.OwnerPlayerId = PlayerId;
                Hitbox.IsLocalPlayer = IsLocal;
            }
        }

        public void BindVisual(RemotePlayerVisual visual)
        {
            Visual = visual;
            if (Visual != null)
            {
                Visual.OwnerPlayerId = PlayerId;
                Visual.ApplyGuildVisual(Guild);
            }
        }
    }
}
