using AggroBird.UnityExtend;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AggroBird.SceneObjects
{
    // Internal hidden object that keeps track of the current scene's GUID
    // All scene objects will register themselves to this object on Awake
    [DefaultExecutionOrder(int.MinValue)]
    internal sealed class SceneGUID : MonoBehaviour
    {
        [SerializeField]
        private GUID sceneGUID;

        private int refCount = 0;

        // List of all currently loaded scenes
        private static readonly Dictionary<Scene, SceneGUID> activeScenes = new();
        internal static Dictionary<Scene, SceneGUID>.ValueCollection AllScenes => activeScenes.Values;
        private static (Scene scene, SceneGUID obj) sceneCache;

        internal static bool TryGetSceneGUIDObject(Scene scene, out SceneGUID sceneObj)
        {
            if (sceneCache.scene.Equals(scene) && sceneCache.obj)
            {
                sceneObj = sceneCache.obj;
                return true;
            }

            if (activeScenes.TryGetValue(scene, out var value))
            {
                sceneObj = value;
                sceneCache = (scene, sceneObj);
                return true;
            }

            sceneObj = default;
            return false;
        }

        // Helper function for getting the GUID of a scene at play time
        // This only works if the scene is currently loaded in
        public static bool TryGetSceneGUID(Scene scene, out GUID guid)
        {
            if (TryGetSceneGUIDObject(scene, out SceneGUID obj))
            {
                guid = obj.sceneGUID;
                return true;
            }
            guid = default;
            return false;
        }

        // All pre-placed scene objects (regular scene objects and scene prefab instances)
        private readonly Dictionary<SceneObjectID, SceneObject> allLocalSceneObjects = new();
        internal Dictionary<SceneObjectID, SceneObject>.ValueCollection AllSceneObjects => allLocalSceneObjects.Values;
        // All pre-placed scene prefab instances, grouped by GUID
        private readonly Dictionary<GUID, Dictionary<SceneObjectID, SceneObject>> allLocalScenePrefabInstances = new();

        // Utility function to get a new ID in case of a collision (Object.Instantiated scene object)
        // Granted instantiation happens after SceneObject.Awake(), all scene objects should have already
        // had their ID's registered
        private ulong lastReassignedObjectId = 0;
        private ulong GetUniquePrefabID(ulong objectId)
        {
            ulong prefabId = ++lastReassignedObjectId;

            // Ensure ID not 0 or in use
            while (prefabId == 0 || allLocalSceneObjects.ContainsKey(new(objectId, prefabId)))
            {
                prefabId++;
            }

            return prefabId;
        }

        internal bool TryFindSceneObject<T>(SceneObjectReference reference, out T result) where T : SceneObject
        {
            if (reference.guid != GUID.zero)
            {
                var sceneObjectId = reference.GetSceneObjectID();
                if (sceneObjectId != SceneObjectID.zero)
                {
                    // Check if object within this scene
                    if (reference.guid == sceneGUID)
                    {
                        // Local pre-placed regular scene object
                        if (allLocalSceneObjects.TryGetValue(sceneObjectId, out SceneObject sceneObject))
                        {
                            if (sceneObject && sceneObject is T casted)
                            {
                                result = casted;
                                return true;
                            }
                        }
                    }
                    else
                    {
                        // Search the prefab instances
                        if (allLocalScenePrefabInstances.TryGetValue(reference.guid, out var table))
                        {
                            if (sceneObjectId.prefabId != 0)
                            {
                                // Specific prefab instance
                                if (table.TryGetValue(sceneObjectId, out SceneObject sceneObject))
                                {
                                    if (sceneObject && sceneObject is T casted)
                                    {
                                        result = casted;
                                        return true;
                                    }
                                }
                            }
                            else
                            {
                                // Any of prefab type
                                foreach (var sceneObject in table.Values)
                                {
                                    if (sceneObject && sceneObject.internalSceneObjectId.objectId == reference.objectId && sceneObject is T casted)
                                    {
                                        result = casted;
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            result = default;
            return false;
        }
        internal void FindSceneObjects<T>(SceneObjectReference reference, List<T> result) where T : SceneObject
        {
            if (reference.guid != GUID.zero)
            {
                var sceneObjectId = reference.GetSceneObjectID();
                if (sceneObjectId != SceneObjectID.zero)
                {
                    // Check if object within this scene
                    if (reference.guid == sceneGUID)
                    {
                        // Local pre-placed regular scene object
                        if (allLocalSceneObjects.TryGetValue(sceneObjectId, out SceneObject sceneObject))
                        {
                            if (sceneObject && sceneObject is T casted)
                            {
                                result.Add(casted);
                            }
                        }
                    }
                    else
                    {
                        // Search the prefab instances
                        if (allLocalScenePrefabInstances.TryGetValue(reference.guid, out var table))
                        {
                            if (sceneObjectId.prefabId != 0)
                            {
                                // Specific prefab instance
                                if (table.TryGetValue(sceneObjectId, out SceneObject sceneObject))
                                {
                                    if (sceneObject && sceneObject is T casted)
                                    {
                                        result.Add(casted);
                                    }
                                }
                            }
                            else
                            {
                                // Any of prefab type
                                foreach (var sceneObject in table.Values)
                                {
                                    if (sceneObject && sceneObject.internalSceneObjectId.objectId == reference.objectId && sceneObject is T casted)
                                    {
                                        result.Add(casted);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        private void RegisterLocalSceneObject(SceneObject sceneObject)
        {
            if (sceneObject.internalSceneObjectGuid != GUID.zero)
            {
                bool isPrefabInstance = sceneObject.internalSceneObjectGuid != sceneGUID;
                if (isPrefabInstance)
                {
                    if (sceneObject.internalSceneObjectId.prefabId == 0)
                    {
                        // Instantiated prefab: get unique new ID
                        sceneObject.internalSceneObjectId.prefabId = GetUniquePrefabID(sceneObject.internalSceneObjectId.objectId);
                    }

                    // Add to prefab instance table
                    if (!allLocalScenePrefabInstances.TryGetValue(sceneObject.internalSceneObjectGuid, out var table))
                    {
                        allLocalScenePrefabInstances[sceneObject.internalSceneObjectGuid] = table = new();
                    }
                    table[sceneObject.internalSceneObjectId] = sceneObject;
                }

                // Add to all objects table
                allLocalSceneObjects[sceneObject.internalSceneObjectId] = sceneObject;

                // Store GUID in scene object
                sceneObject.sceneGUID = sceneGUID;
            }
            else
            {
                // Invalid scene object
                sceneObject.sceneGUID = GUID.zero;
            }
        }
        private void UnregisterLocalSceneObject(SceneObject sceneObject)
        {
            if (allLocalScenePrefabInstances.TryGetValue(sceneObject.internalSceneObjectGuid, out var table))
            {
                table.Remove(sceneObject.internalSceneObjectId);
                if (table.Count == 0)
                {
                    allLocalScenePrefabInstances.Remove(sceneObject.internalSceneObjectGuid);
                }
            }

            allLocalSceneObjects.Remove(sceneObject.internalSceneObjectId);
        }

        internal static void RegisterSceneObject(SceneObject sceneObject)
        {
            if (TryGetSceneGUIDObject(sceneObject.gameObject.scene, out SceneGUID sceneGUIDObj))
            {
                // Register this object to the scene that its currently in
                sceneGUIDObj.RegisterLocalSceneObject(sceneObject);
            }
            else
            {
                // Failed to find scene object
                sceneObject.sceneGUID = GUID.zero;
            }
        }
        internal static void UnregisterSceneObject(SceneObject sceneObject)
        {
            if (TryGetSceneGUIDObject(sceneObject.gameObject.scene, out SceneGUID sceneGUIDObj))
            {
                sceneGUIDObj.UnregisterLocalSceneObject(sceneObject);
            }
        }


        private void Awake()
        {
            if (sceneGUID != GUID.zero)
            {
                var scene = gameObject.scene;
                if (scene.IsValid())
                {
                    if (activeScenes.TryGetValue(scene, out var value))
                    {
                        Debug.LogError($"Scene '{scene.name}' is being loaded additively multiple times. This may cause problems with scene object identification.", this);
                        value.refCount++;
                    }
                    else
                    {
                        activeScenes[scene] = this;
                        refCount = 1;
                    }
                }
            }
        }
        private void OnDestroy()
        {
            if (sceneGUID != GUID.zero)
            {
                var scene = gameObject.scene;
                if (scene.IsValid())
                {
                    if (activeScenes.TryGetValue(scene, out var value))
                    {
                        if (value.refCount == 1)
                        {
                            activeScenes.Remove(scene);
                        }
                        else
                        {
                            value.refCount--;
                        }
                    }
                }
            }
        }

#if UNITY_EDITOR
        private bool isPendingValidation = false;
        private void ValidateDelayed()
        {
            UnityEditor.EditorApplication.delayCall -= ValidateDelayed;
            isPendingValidation = false;

            if (!this)
            {
                return;
            }

            var globalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this);
            if (globalObjectId.identifierType == 2)
            {
                GUID sceneGUID = new(globalObjectId.assetGUID.ToString());
                if (this.sceneGUID != sceneGUID)
                {
                    this.sceneGUID = sceneGUID;
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
            else
            {
                if (sceneGUID != GUID.zero)
                {
                    sceneGUID = GUID.zero;
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
        }
        private void OnValidate()
        {
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!isPendingValidation && gameObject.scene.IsValid())
                {
                    isPendingValidation = true;
                    UnityEditor.EditorApplication.delayCall += ValidateDelayed;
                }
            }
        }
#endif
    }
}