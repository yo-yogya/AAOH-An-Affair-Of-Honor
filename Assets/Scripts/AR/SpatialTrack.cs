using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Spatial tracking data stream for a player entity.
    /// Explicitly implements the Spatial Proxy Model:
    /// Phone AR Pose -> Spatial Track (Player Proxy) -> Remote Player Visual / Hitbox.
    /// Keeps raw phone pose separate from logical player proxy position.
    /// </summary>
    [Serializable]
    public class SpatialTrack
    {
        public string playerId;
        public Vector3 rawLocalPosition = Vector3.zero;
        public Quaternion rawLocalRotation = Quaternion.identity;

        // Position and rotation relative to the shared physical reference battle-space
        public Vector3 sharedPosition = Vector3.zero;
        public Quaternion sharedRotation = Quaternion.identity;

        public ARTrackingConfidence confidence = ARTrackingConfidence.None;
        public float lastUpdatedTime = 0f;
        public bool isTracked = false;

        public SpatialTrack(string id)
        {
            playerId = id;
            confidence = ARTrackingConfidence.None;
            isTracked = false;
        }

        public void UpdateLocalPose(Vector3 localPos, Quaternion localRot, ARTrackingConfidence conf, float timestamp)
        {
            rawLocalPosition = localPos;
            rawLocalRotation = localRot;
            confidence = conf;
            isTracked = conf != ARTrackingConfidence.None;
            lastUpdatedTime = timestamp;
        }

        public void UpdateSharedPose(Vector3 sharedPos, Quaternion sharedRot, ARTrackingConfidence conf, float timestamp)
        {
            sharedPosition = sharedPos;
            sharedRotation = sharedRot;
            confidence = conf;
            isTracked = conf != ARTrackingConfidence.None;
            lastUpdatedTime = timestamp;
        }
    }
}
