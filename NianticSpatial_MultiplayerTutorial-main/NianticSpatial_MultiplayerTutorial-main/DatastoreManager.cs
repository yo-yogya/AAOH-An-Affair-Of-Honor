using System;
using System.Collections.Generic;
using System.Text;
using Niantic.Lightship.SharedAR.Datastore;
using Niantic.Lightship.SharedAR.Networking;
using UnityEngine;

/// <summary>
/// DatastoreManager
/// Handles storing objects in the data store and syncing them to all connected clients.
/// </summary>
public class DatastoreManager : MonoBehaviour
{
    //struct to hold data we we want to store
    public struct ObjectData
    {
        public Vector3 _position;
        public Color _color;
        public byte[] _data;
    };

    public struct PlayerData
    {
        public Vector3 position;
        public string peerId;
    }
    
    [Header("Managers")]
    [SerializeField]
    private RoomManager _roomManager;

    [SerializeField]
    private Tracker _tracker;
    
    [SerializeField]
    private Mapper _mapper;
    
    private IDatastore _datastore;

    public INetworking _network
    {
        get;
        private set;
    }
    
    public bool _networkRunning;
    private bool _inRoom=false;
    
    private const string PlayerKeyPrefix = "PLAYER_";
    private const int PlayerDatastoreIndex = 6;
    private const float PlayerMoveSpeed = 4f;

    private readonly Dictionary<string, GameObject> _playerInstances = new(); 
    private readonly Dictionary<string, Vector3> _playerTargetPositions = new();
    
    
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Material player1Material;
    [SerializeField] Material player2Material;


    public event Action<bool> OnPlayerCharacterPlaced;
    
    /// <summary>
    /// CreateOrJoinDataStore
    /// Will create new network Room and Datastore based on the string name you provice
    /// This is a global room name for the current API key.
    /// </summary>
    /// <param name="storeName"></param>
    public void CreateOrJoinDataStore(string storeName)
    {
        //am i already in the room
        if (_inRoom == true)
            return;
        
        _inRoom = true;
        
        //make a new room
        _roomManager.JoinRoomByName(storeName);

        if (_roomManager.Room == null)
            Debug.Log( "Network not found, do you have internet? and is your api key set");
        
        //cache datastore & network for easy access.
        _datastore = _roomManager.Room.Datastore;
        _network = _roomManager.Room.Networking;
        
        //hook callbacks to manage the data store
        _datastore.DatastoreCallback += DatastoreUpdated;
        _network.NetworkEvent += NetworkUpdate;
        
        //start the network to enable messaging.
        _network.Join();
    }

    /// <summary>
    /// LeaveDataStore
    /// leave the room and clean up the client side.
    /// </summary>
    public void LeaveDataStore()
    {
        _roomManager.LeaveRoom();
        _network?.Leave();
        
        if(_datastore != null)
            _datastore.DatastoreCallback -= DatastoreUpdated;
        
        if(_network != null)
            _network.NetworkEvent -= NetworkUpdate;
        
        _datastore = null;
        _network = null;
        _inRoom = false;
        
        _playerInstances.Clear();
        _playerTargetPositions.Clear();
    }

    public void PlacePlayerCharacterAtPosition(Vector3 hitPosition, PeerID peerId)
    {
        SetPlayerCharacterTransform(hitPosition, peerId, true);
    }

    public void MovePlayerCharacterToPosition(Vector3 hitPosition, PeerID peerId)
    {
        SetPlayerCharacterTransform(hitPosition, peerId, false);
    }

    public bool HasPlayerForPeer(PeerID peerId)
    {
        var key = GetPlayerKey(peerId);
        if (_playerInstances.TryGetValue(key, out var instance) && instance)
        {
            return true; 
        }

        _playerInstances.Remove(key);
        _playerTargetPositions.Remove(key);
        return false;

    }
    
    /// <summary>
    /// This function listens for data store update events and as it is running on the client
    /// we can simply add/delete/update our local version of the stored items to match the server state.
    /// </summary>
    /// <param name="args"></param>
    private void DatastoreUpdated(DatastoreCallbackArgs args)
    {
        //as a simple way to identify the message type in the data store we are adding a prefix to the messages.
        //e.g. CUBE_ MAP_ this helps us know what type of object we are receiving a message about.
        if (!args.Key.StartsWith("MAP") && !args.Key.StartsWith("CUBE") && !args.Key.StartsWith(PlayerKeyPrefix))
            return;
        
        // newly added or changed
        if (args.OperationType == DatastoreOperationType.ServerChangeUpdated)
        {
            var dataAsString = Encoding.Unicode.GetString(args.Value);
            var objectData = JsonUtility.FromJson<ObjectData>(dataAsString);
            
            //is this a the map?
            if (args.Key.StartsWith("MAP"))
            {
                //load this map
                _tracker.LoadMap(objectData._data);
                return; 
            }

            if (args.Key.StartsWith(PlayerKeyPrefix))
            {
                var playerData = JsonUtility.FromJson<PlayerData>(dataAsString);
                HandlePlayerDatastoreUpdate(args.Key, playerData);
                return;
            }

        }
        else if (args.OperationType == DatastoreOperationType.ServerChangeDeleted)
        {
            //an object has been deleted from the server so we need to clean up the client.
            //find the correct root.
            var anchor = _tracker.Anchor;
            if (_tracker.Anchor)
            {
                var ob = anchor.transform.Find(args.Key);
                if (ob)
                {
                    Destroy(ob.gameObject);
                }
            }

            if (args.Key.StartsWith(PlayerKeyPrefix))
            {
                _playerInstances.Remove(args.Key);
                _playerTargetPositions.Remove(args.Key);
                if(_network != null && args.Key == GetPlayerKey(_network.SelfPeerID))
                {
                    OnPlayerCharacterPlaced?.Invoke(false);
                }

            }
        }
    }

    private void HandlePlayerDatastoreUpdate(string dataStoreKey, PlayerData playerData)
    {
        var playerInstance = EnsurePlayerInstance(dataStoreKey, playerData.peerId);
        if (!playerInstance)
        {
            return;
        }
        
        var isFirstUpdate = !_playerTargetPositions.ContainsKey(dataStoreKey);
        _playerTargetPositions[dataStoreKey] = playerData.position;

        if (isFirstUpdate)
        {
            playerInstance.transform.localPosition = playerData.position;
        }

        ApplyPlayerMaterial(playerInstance, dataStoreKey, playerData.peerId);

        if (_network != null && dataStoreKey == GetPlayerKey(_network.SelfPeerID))
        {
            OnPlayerCharacterPlaced?.Invoke(true);
        }
    }

    private string GetPlayerKey(PeerID peerId)
    {
        return $"{PlayerKeyPrefix}{peerId.ToString()}";
    }

    private GameObject EnsurePlayerInstance(string dataStoreKey, string serializedpeerId)
    {
        if (_playerInstances.TryGetValue(dataStoreKey, out var existingInstance) && existingInstance)
        {
            return existingInstance;
        }

        if (!playerPrefab)
        {
            Debug.LogError("PlayerPrefab not set");
            return null;
        }

        var instance = Instantiate(playerPrefab);
        instance.name = dataStoreKey;
        instance.transform.SetParent(_tracker.Anchor,false);
        _playerInstances[dataStoreKey] = instance;
        
        ApplyPlayerMaterial(instance, dataStoreKey, serializedpeerId);
        return instance;
    }


    private void ApplyPlayerMaterial(GameObject playerInstance, string dataStoreKey, string serializedpeerId)
    {
        if (!playerInstance)
        {
            return;
        }

        var renderer = playerInstance.GetComponent<Renderer>();
        if (!renderer)
        {
            Debug.LogWarning("Player instance does not have a renderer");
            return;
        }
        
        var materialToApply = GetMaterialForPeer(serializedpeerId);
        if (!materialToApply)
        {
            Debug.LogWarning(" Could not find material for peer " + serializedpeerId);
            return;
        }
        
        renderer.sharedMaterial = materialToApply;
    }

    private Material GetMaterialForPeer(string serializedpeerId)
    {
        if (string.IsNullOrEmpty(serializedpeerId))
        {
            return player2Material;
        }


        bool usePrimaryMaterial = serializedpeerId == _network.PeerIDs[0].ToString();
        return usePrimaryMaterial ? player1Material : player2Material;
    }

    private void SetPlayerCharacterTransform(Vector3 hitPosition, PeerID peerId, bool isInitialPlacement)
    {
        if (_datastore == null)
        {
            Debug.LogWarning("Datastore not initialised");
            return;
        }
        
        var anchor = _tracker.Anchor;

        if (!anchor)
        {
            Debug.LogWarning("No anchor found");
            return;
        }

        var peerIdString = peerId.ToString();
        var dataStoreKey = GetPlayerKey(peerId);

        var localPosition = anchor.InverseTransformPoint(hitPosition);
        var playInstance = EnsurePlayerInstance(dataStoreKey, peerIdString);

        if (!playInstance)
        {
            return;
        }

        if (playInstance.transform.parent != anchor)
        {
            playInstance.transform.SetParent(anchor, false);
        }

        var shouldSnap = !_playerTargetPositions.ContainsKey(dataStoreKey) || isInitialPlacement;
        _playerTargetPositions[dataStoreKey] = localPosition;

        if (shouldSnap)
        {
            playInstance.transform.localPosition = localPosition;
        }
        
        ApplyPlayerMaterial(playInstance, dataStoreKey, peerIdString);
        
        var playerData = new PlayerData()
        {
            position = localPosition,
            peerId = peerIdString
        };

        var payload = Encoding.Unicode.GetBytes(JsonUtility.ToJson(playerData));
        _datastore.SetData(PlayerDatastoreIndex, dataStoreKey, payload);

        if (isInitialPlacement)
        {
            OnPlayerCharacterPlaced?.Invoke(true);       
        }

    }    

    /// <summary>
    /// NetworkUpdate
    /// Hooking this callback just to know everything is initialised.
    /// </summary>
    /// <param name="obj"></param>
    private void NetworkUpdate(NetworkEventArgs obj)
    { 
        switch(obj.networkEvent)
        {
            case NetworkEvents.Connected:
                _networkRunning = true;
                break;
            case NetworkEvents.ArdkShutdown:
                _networkRunning = false;
            break;
        }
    }

    /// <summary>
    /// OnDestroy
    /// clear up on exit
    /// </summary>
    private void OnDestroy()
    {
      LeaveDataStore();
    }
    
    /// <summary>
    /// CreateAndPlaceCube
    /// Manging the cube placement/storage and anchoring to map function
    /// </summary>
    /// <param name="localPos"></param>
    /// <param name="color"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public GameObject CreateAndPlaceCube(Vector3 localPos, Color color, string name = "Cube")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.GetComponent<Renderer>().material.color = color;
        
        //add it under the anchor on our map.
        _tracker.AddObjectToAnchor(go);
        go.transform.localPosition = localPos;
        //make it smaller.
        go.transform.localScale = new Vector3(0.2f,0.2f,0.2f);
        return go;
    }

    private void Update()
    {
        if (_playerTargetPositions.Count == 0)
        {
            return; 
        }
        
        var anchor = _tracker.Anchor;
        var step = PlayerMoveSpeed * Time.deltaTime;

        if (step <= 0f)
        {
            return;
        }

        foreach (var kvp in _playerTargetPositions)
        {
            if (!_playerInstances.TryGetValue(kvp.Key, out var playerInstance))
            {
                continue;
            }

            if (!playerInstance)
            {
                continue;
            }

            if (!anchor)
            {
                playerInstance.transform.localPosition = kvp.Value;
                continue;
            }
            
            var targetWorld = anchor.TransformPoint(kvp.Value);
            var currentWorld = playerInstance.transform.position;

            if ((currentWorld - targetWorld).sqrMagnitude < 0.0001f)
            {
                playerInstance.transform.position = targetWorld;
                continue; 
            }

            var newWorld = Vector3.MoveTowards(currentWorld, targetWorld, step);
            playerInstance.transform.position = newWorld;
        }
    }

    /// <summary>
    /// PlaceCube
    /// Will place a colored cube and store it in the datastore
    /// </summary>
    public void PlaceCube()
    {
        //place a cube 2m in front of the camera.
        var pos = Camera.main.transform.position + (Camera.main.transform.forward*2.0f);
        
        //each cube will have a unique name so you can update or delete them on the data store.
        var cubeName = "CUBE_" +  Guid.NewGuid().ToString();
        
        //randomise cube color for each peer
        var color = Color.magenta;
        if (_inRoom)
        {
            Color []colors =
            {
                Color.red,
                Color.green,
                Color.blue,
                Color.black,
                Color.grey, 
                Color.cyan,
                Color.white,
                Color.yellow
            };
            //will pick a color based on your network id.
            int code = Math.Abs(_network.SelfPeerID.GetHashCode());
            code = code % (colors.Length-1);
            color = colors[code];
        }
        
        //place a cube in the scene
        var go = CreateAndPlaceCube(_tracker.GetAnchorRelativePosition(pos),color, cubeName);
        
        //store it in the data store.
        DatastoreManager.ObjectData objectData = new DatastoreManager.ObjectData();
        objectData._position = go.transform.localPosition;
        objectData._color = color;
        
        //turn it into bytes and send it datastore.
        string json = JsonUtility.ToJson(objectData);
        _datastore.SetData(1, cubeName,Encoding.Unicode.GetBytes(json));
    }
    
    /// <summary>
    /// DeleteCubes
    /// deletes all cubes from the data store
    /// </summary>
    public void DeleteCubes()
    {
        //delete from in game.
        if (_tracker.Anchor)
        {
            for (int i = 0; i < _tracker.Anchor.transform.childCount; i++)
            {
                Destroy(_tracker.Anchor.transform.GetChild(i).gameObject);
                _datastore.DeleteData(3, _tracker.Anchor.transform.GetChild(i).name);
            }
        }
    }

    /// <summary>
    /// SaveMapToDatastore
    /// Will save the current map to the data store
    /// </summary>
    public void SaveMapToDatastore()
    {
        ObjectData objectData = new ObjectData();
        objectData._data = _mapper.GetMap().Serialize();
                    
        string json = JsonUtility.ToJson(objectData);
        _datastore.SetData(2, "MAP_DeviceMap", Encoding.Unicode.GetBytes(json));
    }
}
