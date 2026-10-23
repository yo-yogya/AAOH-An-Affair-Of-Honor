using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Encapsulates the coordinate transformation between a device's Local AR Space
    /// and the physical Shared Battle Space established via shared calibration.
    ///
    /// Mathematical mapping:
    /// P_shared = R_origin^-1 * (P_local - T_origin)
    /// P_remoteLocal = (R_origin,peer * P_shared) + T_origin,peer
    /// </summary>
    [Serializable]
    public class SharedBattleSpace
    {
        [SerializeField] private Vector3 _originPosition = Vector3.zero;
        [SerializeField] private Quaternion _originRotation = Quaternion.identity;
        [SerializeField] private bool _isOriginSet = false;

        public Vector3 OriginPosition => _originPosition;
        public Quaternion OriginRotation => _originRotation;
        public bool IsOriginSet => _isOriginSet;

        public SharedBattleSpace()
        {
            Reset();
        }

        public void SetOrigin(Vector3 localPosition, Quaternion localRotation)
        {
            _originPosition = localPosition;
            _originRotation = localRotation;
            _isOriginSet = true;
        }

        public void Reset()
        {
            _originPosition = Vector3.zero;
            _originRotation = Quaternion.identity;
            _isOriginSet = false;
        }

        /// <summary>
        /// Pure C# quaternion inversion to ensure safety across Unity runtime and standalone test environments.
        /// </summary>
        public static Quaternion InvertQuaternion(Quaternion q)
        {
            float lengthSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (lengthSq < 0.000001f) return Quaternion.identity;
            float invNorm = 1.0f / lengthSq;
            return new Quaternion(-q.x * invNorm, -q.y * invNorm, -q.z * invNorm, q.w * invNorm);
        }

        /// <summary>
        /// Pure C# vector rotation by quaternion to ensure safety across Unity runtime and standalone test environments.
        /// </summary>
        public static Vector3 RotateVector(Quaternion q, Vector3 v)
        {
            float tx = 2f * (q.y * v.z - q.z * v.y);
            float ty = 2f * (q.z * v.x - q.x * v.z);
            float tz = 2f * (q.x * v.y - q.y * v.x);

            return new Vector3(
                v.x + q.w * tx + (q.y * tz - q.z * ty),
                v.y + q.w * ty + (q.z * tx - q.x * tz),
                v.z + q.w * tz + (q.x * ty - q.y * tx)
            );
        }

        /// <summary>
        /// Pure C# quaternion multiplication to ensure safety across Unity runtime and standalone test environments.
        /// </summary>
        public static Quaternion MultiplyQuaternions(Quaternion lhs, Quaternion rhs)
        {
            return new Quaternion(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z
            );
        }

        /// <summary>
        /// Converts a local AR world position to coordinates relative to the shared battle origin.
        /// </summary>
        public Vector3 LocalToSharedPosition(Vector3 localPosition)
        {
            if (!_isOriginSet) return localPosition;
            Quaternion invOrigin = InvertQuaternion(_originRotation);
            return RotateVector(invOrigin, localPosition - _originPosition);
        }

        /// <summary>
        /// Converts a local AR world rotation to a rotation relative to the shared battle orientation.
        /// </summary>
        public Quaternion LocalToSharedRotation(Quaternion localRotation)
        {
            if (!_isOriginSet) return localRotation;
            Quaternion invOrigin = InvertQuaternion(_originRotation);
            return MultiplyQuaternions(invOrigin, localRotation);
        }

        /// <summary>
        /// Converts incoming shared coordinates into this device's local AR world position.
        /// </summary>
        public Vector3 SharedToLocalPosition(Vector3 sharedPosition)
        {
            if (!_isOriginSet) return sharedPosition;
            return RotateVector(_originRotation, sharedPosition) + _originPosition;
        }

        /// <summary>
        /// Converts incoming shared rotation into this device's local AR world rotation.
        /// </summary>
        public Quaternion SharedToLocalRotation(Quaternion sharedRotation)
        {
            if (!_isOriginSet) return sharedRotation;
            return MultiplyQuaternions(_originRotation, sharedRotation);
        }
    }
}
