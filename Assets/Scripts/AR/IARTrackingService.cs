using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    public enum ARTrackingConfidence
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public struct ARTargetStatus
    {
        public bool isVisible;
        public ARTrackingConfidence confidence;
        public Vector3 estimatedPosition;
        public Quaternion estimatedOrientation;
        public float distanceMeters;
        public bool isHitboxAimed;
    }

    /// <summary>
    /// Abstract interface for AR Tracking and Opponent Localization.
    /// Shields the battle engine from specific AR Foundation / CV implementations.
    /// </summary>
    public interface IARTrackingService
    {
        bool IsARInitialized { get; }
        bool IsCalibrated { get; }
        ARTargetStatus TargetStatus { get; }

        event Action<ARTargetStatus> OnTargetStatusUpdated;
        event Action OnTargetAcquired;
        event Action OnTargetLost;
        event Action OnTargetReacquired;

        void CalibrateSharedBattleSpace(Action<bool, string> onComplete);
        bool RaycastAimAtHitbox(Vector2 screenCenter);
        void SimulateTargetAcquisition(Vector3 position, float distanceMeters);
        void SimulateTargetLost();
        void SimulateTargetReacquired();
    }

    /// <summary>
    /// Contract for two-device shared battle space calibration.
    /// Allows future integration of spatial anchors, image targets, or GPS/compass correlation.
    /// </summary>
    public interface ISharedBattleSpaceProvider
    {
        bool IsSpaceCalibrated { get; }
        Vector3 WorldOrigin { get; }
        Quaternion WorldOrientation { get; }

        void EstablishSpatialOrigin(Action<bool, string> onComplete);
        void AlignWithPeer(string peerAnchorData, Action<bool, string> onComplete);
    }
}
