using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class PlaceControlPlayer : MonoBehaviour
{
    [SerializeField] private DatastoreManager datastoreManager;
    [SerializeField] private ARRaycastManager raycastManager;

    private List<ARRaycastHit> _raycastHits = new();
    private bool playerHasBeenPlaced = false;


    private void OnEnable()
    {
        TouchSimulation.Enable();
        EnhancedTouchSupport.Enable();

        if (datastoreManager != null)
        {
            datastoreManager.OnPlayerCharacterPlaced += HandlePlayerCharacterPlaced;

            if (datastoreManager._network != null)
            {
                playerHasBeenPlaced = datastoreManager.HasPlayerForPeer(datastoreManager._network.SelfPeerID);
            }
        }
        
    }

    private void OnDisable()
    {
        TouchSimulation.Disable();
        EnhancedTouchSupport.Disable();
        
        if (datastoreManager != null)
        {
            datastoreManager.OnPlayerCharacterPlaced -= HandlePlayerCharacterPlaced;
        }
    }

    private void HandlePlayerCharacterPlaced(bool placed)
    {
        playerHasBeenPlaced = placed;
    }

    private void Update()
    {
        if(datastoreManager == null || !datastoreManager._networkRunning || datastoreManager._network == null) return;


        if (!TryGetPointerPosition(out var pointerPosition, out var pointerId, out var isMouseInput))
        {
            return; 
        }

        if (EventSystem.current != null)
        {
            var pointerOverUI = isMouseInput 
                ? EventSystem.current.IsPointerOverGameObject(PointerInputModule.kMouseLeftId)
                : EventSystem.current.IsPointerOverGameObject(pointerId);

            if (pointerOverUI)
            {
                return; 
            }
        }

        if (!TryRaycastTouchPosition(pointerPosition, out var hitPose))
        {
            return;
        }

        var peerId = datastoreManager._network.SelfPeerID;
        if (!playerHasBeenPlaced)
        {
            datastoreManager.PlacePlayerCharacterAtPosition(hitPose.position, peerId);
            return;
        }
        
        datastoreManager.MovePlayerCharacterToPosition(hitPose.position, peerId);

    }

    private bool TryRaycastTouchPosition(Vector2 touchPosition, out Pose poseInfo)
    {
        poseInfo = default;

        if (!raycastManager)
        {
            return false;
        }
        
        _raycastHits.Clear();
        
        if(raycastManager.Raycast(touchPosition, _raycastHits, TrackableType.AllTypes) && _raycastHits.Count > 0)
        {
            poseInfo = _raycastHits[0].pose;
            return true;
        }

        return false;
    }

    private bool TryGetPointerPosition(out Vector2 pointerPosition, out int pointerId, out bool isMouseInput)
    {
        pointerPosition = default;
        pointerId = PointerInputModule.kMouseLeftId;
        isMouseInput = false;

        for (int i = 0; i < Touch.activeTouches.Count; i++)
        {
            var activeTouch = Touch.activeTouches[i];
            if (activeTouch.phase == UnityEngine.InputSystem.TouchPhase.Canceled ||
                activeTouch.phase == TouchPhase.Ended
               )
            {
                continue;
            }

            pointerPosition = activeTouch.screenPosition;
            pointerId = activeTouch.touchId;
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            isMouseInput = true;
            return true;
        }
        
        return false;
    }
}
