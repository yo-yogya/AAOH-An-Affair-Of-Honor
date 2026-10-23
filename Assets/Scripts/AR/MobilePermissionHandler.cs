using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Handles mobile runtime permissions for Camera (at AR initialization)
    /// and Microphone (deferred until voice summoning feature is activated).
    /// Provides clear user-facing failure explanations when permissions are denied.
    /// </summary>
    public class MobilePermissionHandler : MonoBehaviour
    {
        public static MobilePermissionHandler Instance { get; private set; }

        public bool HasCameraPermission { get; private set; } = false;
        public bool HasMicrophonePermission { get; private set; } = false;

#pragma warning disable CS0067
        public event Action<string, string> OnPermissionDenied;
#pragma warning restore CS0067

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        public void RequestCameraPermission(Action<bool> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                HasCameraPermission = true;
                onResult?.Invoke(true);
                return;
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += (perm) =>
            {
                HasCameraPermission = true;
                onResult?.Invoke(true);
            };
            callbacks.PermissionDenied += (perm) =>
            {
                HasCameraPermission = false;
                OnPermissionDenied?.Invoke("Camera", "Camera permission is required to track the AR battlefield and locate your opponent.");
                onResult?.Invoke(false);
            };
            callbacks.PermissionDeniedAndDontAskAgain += (perm) =>
            {
                HasCameraPermission = false;
                OnPermissionDenied?.Invoke("Camera", "Camera permission was permanently denied. Please enable camera access in Android Device Settings.");
                onResult?.Invoke(false);
            };

            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
#else
            HasCameraPermission = true;
            onResult?.Invoke(true);
#endif
        }

        public void RequestMicrophonePermission(Action<bool> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                HasMicrophonePermission = true;
                onResult?.Invoke(true);
                return;
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += (perm) =>
            {
                HasMicrophonePermission = true;
                onResult?.Invoke(true);
            };
            callbacks.PermissionDenied += (perm) =>
            {
                HasMicrophonePermission = false;
                OnPermissionDenied?.Invoke("Microphone", "Microphone access is needed for voice spell summoning. You may use touch buttons as a fallback.");
                onResult?.Invoke(false);
            };
            callbacks.PermissionDeniedAndDontAskAgain += (perm) =>
            {
                HasMicrophonePermission = false;
                OnPermissionDenied?.Invoke("Microphone", "Microphone access was denied. You can still play using touch button selection.");
                onResult?.Invoke(false);
            };

            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
#else
            HasMicrophonePermission = true;
            onResult?.Invoke(true);
#endif
        }
    }
}
