using AggroBird.UnityExtend;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using AggroBird.SceneObjects.Editor;
#endif

[assembly: InternalsVisibleTo("AggroBird.SceneObjects.Editor")]

namespace AggroBird.SceneObjects
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal sealed class SceneObjectGUIDAttribute : PropertyAttribute
    {

    }

    [Serializable]
    internal struct SceneObjectID
    {
        public static readonly SceneObjectID zero = new();

        public SceneObjectID(ulong objectId, ulong prefabId)
        {
            this.objectId = objectId;
            this.prefabId = prefabId;
        }

        public ulong objectId;
        public ulong prefabId;

        public readonly bool Equals(SceneObjectID other)
        {
            return objectId == other.objectId && prefabId == other.prefabId;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is SceneObjectID other && Equals(other);
        }
        public override int GetHashCode()
        {
            return objectId.GetHashCode() ^ (prefabId.GetHashCode() << 2);
        }

        public static bool operator ==(SceneObjectID lhs, SceneObjectID rhs)
        {
            return lhs.objectId == rhs.objectId && lhs.prefabId == rhs.prefabId;
        }
        public static bool operator !=(SceneObjectID lhs, SceneObjectID rhs)
        {
            return lhs.objectId != rhs.objectId || lhs.prefabId != rhs.prefabId;
        }
    }

    public abstract class SceneObject : MonoBehaviour
    {
        // In the case of a regular scene object, this contains the scene GUID
        // In the case of a scene prefab instance, this contains the prefab GUID
        [SerializeField, SceneObjectGUID] internal GUID internalSceneObjectGuid;
        // For regular scene objects, objectId will be the scene object ID and prefabId will be 0
        // For prefab instances, objectId will be the scene object ID within the prefab and prefabId will be prefab instance ID
        [SerializeField] internal SceneObjectID internalSceneObjectId;

        // The GUID of the scene this object is in at play time (zero if registration failed)
        internal GUID sceneGUID;

        // Check if this object is referenced by the provided reference
        // (Either its part of the same prefab family, or it is the actual scene object pointed to)
        public bool IsReferenced(SceneObjectReference reference)
        {
            if (reference.guid != GUID.zero && sceneGUID != GUID.zero)
            {
                if (reference.prefabId == 0)
                {
                    // Prefab type comparison
                    return reference.guid == internalSceneObjectGuid;
                }
                else
                {
                    // Specific scene object comparison, ensure its a object within this scene
                    // (Don't use the object's GUID, it might be a prefab)
                    if (reference.guid == sceneGUID)
                    {
                        return reference.GetSceneObjectID() == internalSceneObjectId;
                    }
                }
            }

            return false;
        }

        // Get a reference for later identification
        // Returns an invalid reference if the scene is not playing or if the object is an uninstantiated prefab
        public SceneObjectReference GetReference()
        {
            // Ensure valid
            if (sceneGUID == GUID.zero || internalSceneObjectId.objectId == 0)
            {
                return default;
            }

            // Ensure instantiated if prefab
            if (sceneGUID != internalSceneObjectGuid && internalSceneObjectId.prefabId == 0)
            {
                return default;
            }

            return new(sceneGUID, internalSceneObjectId.objectId, internalSceneObjectId.prefabId);
        }


        private static class ListBuffer<T>
        {
            private static readonly List<T> list = new();
            public static List<T> Get()
            {
                list.Clear();
                return list;
            }
        }

        // Find first scene object that matches reference within all current scenes
        public static bool TryFindSceneObject<T>(SceneObjectReference reference, out T result) where T : SceneObject
        {
            if (reference.HasValue())
            {
                foreach (var sceneGUIDObj in SceneGUID.AllScenes)
                {
                    if (sceneGUIDObj.TryFindSceneObject(reference, out result))
                    {
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }
        public static bool TryFindSceneObject<T>(SceneObjectReference<T> reference, out T result) where T : SceneObject
        {
            return TryFindSceneObject((SceneObjectReference)reference, out result);
        }

        // Find first scene object that matches reference within a specific scene
        public static bool TryFindSceneObject<T>(Scene scene, SceneObjectReference reference, out T result) where T : SceneObject
        {
            if (SceneGUID.TryGetSceneGUIDObject(scene, out var sceneGUIDObj))
            {
                return sceneGUIDObj.TryFindSceneObject(reference, out result);
            }
            result = default;
            return false;
        }
        public static bool TryFindSceneObject<T>(Scene scene, SceneObjectReference<T> reference, out T result) where T : SceneObject
        {
            return TryFindSceneObject(scene, (SceneObjectReference)reference, out result);
        }

        // Find all scene objects that match reference within all current scenes
        public static T[] FindSceneObjects<T>(SceneObjectReference reference) where T : SceneObject
        {
            List<T> list = ListBuffer<T>.Get();
            FindSceneObjects(reference, list);
            return list.ToArray();
        }
        public static T[] FindSceneObjects<T>(SceneObjectReference<T> reference) where T : SceneObject
        {
            List<T> list = ListBuffer<T>.Get();
            FindSceneObjects((SceneObjectReference)reference, list);
            return list.ToArray();
        }
        public static void FindSceneObjects<T>(SceneObjectReference reference, List<T> result) where T : SceneObject
        {
            result.Clear();

            if (reference.HasValue())
            {
                foreach (var sceneGUIDObj in SceneGUID.AllScenes)
                {
                    sceneGUIDObj.FindSceneObjects(reference, result);
                }
            }
        }
        public static void FindSceneObjects<T>(SceneObjectReference<T> reference, List<T> result) where T : SceneObject
        {
            FindSceneObjects((SceneObjectReference)reference, result);
        }

        // Find all scene objects that match reference within a specific scene
        public static T[] FindSceneObjects<T>(Scene scene, SceneObjectReference reference) where T : SceneObject
        {
            List<T> list = ListBuffer<T>.Get();
            FindSceneObjects(scene, reference, list);
            return list.ToArray();
        }
        public static T[] FindSceneObjects<T>(Scene scene, SceneObjectReference<T> reference) where T : SceneObject
        {
            List<T> list = ListBuffer<T>.Get();
            FindSceneObjects(scene, (SceneObjectReference)reference, list);
            return list.ToArray();
        }
        public static void FindSceneObjects<T>(Scene scene, SceneObjectReference reference, List<T> result) where T : SceneObject
        {
            result.Clear();

            if (SceneGUID.TryGetSceneGUIDObject(scene, out var sceneGUIDObj))
            {
                sceneGUIDObj.FindSceneObjects(reference, result);
            }
        }
        public static void FindSceneObjects<T>(Scene scene, SceneObjectReference<T> reference, List<T> result) where T : SceneObject
        {
            FindSceneObjects(scene, (SceneObjectReference)reference, result);
        }


        protected virtual void Awake()
        {
            SceneGUID.RegisterSceneObject(this);
        }
        protected virtual void OnDestroy()
        {
            if (sceneGUID != GUID.zero)
            {
                SceneGUID.UnregisterSceneObject(this);
            }
        }

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (!pendingValidation)
            {
                if (objectsPendingValidation.Count == 0)
                {
                    UnityEditor.EditorApplication.delayCall += ValidateDelayed;
                }
                objectsPendingValidation.Add(this);
                pendingValidation = true;
            }
#endif
        }

#if UNITY_EDITOR
        private static readonly string GUIDUpperPropertyPath = $"{nameof(internalSceneObjectGuid)}.{Utility.GetPropertyBackingFieldName("Upper")}";
        private static readonly string GUIDLowerPropertyPath = $"{nameof(internalSceneObjectGuid)}.{Utility.GetPropertyBackingFieldName("Lower")}";
        private static bool IsGUIDModified(SceneObject obj)
        {
            foreach (var modification in UnityEditor.PrefabUtility.GetPropertyModifications(obj))
            {
                if (modification.propertyPath == GUIDUpperPropertyPath || modification.propertyPath == GUIDLowerPropertyPath)
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly List<SceneObject> objectsPendingValidation = new();
        private static readonly HashSet<Scene> scenesPendingValidation = new();
        private bool pendingValidation = false;
        private void ValidateSceneObject(bool isPlaying)
        {
            GameObject go = gameObject;

            // Skip prefab stage assets for now
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go) != null)
            {
                return;
            }

            if (UnityEditor.EditorUtility.IsPersistent(go))
            {
                var globalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this);
                if (globalObjectId.identifierType == 1)
                {
                    // Reset GUID to asset and clear object ID on original prefabs
                    GUID assetGUID = new(globalObjectId.assetGUID.ToString());
                    SceneObjectID sceneObjectID = globalObjectId.GetSceneObjectID();
                    if (assetGUID != internalSceneObjectGuid || internalSceneObjectId != sceneObjectID)
                    {
                        internalSceneObjectGuid = assetGUID;
                        internalSceneObjectId = sceneObjectID;
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
                else if (internalSceneObjectGuid != GUID.zero || internalSceneObjectId != SceneObjectID.zero)
                {
                    // Clear everything on invalid objects
                    internalSceneObjectGuid = GUID.zero;
                    internalSceneObjectId = SceneObjectID.zero;
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
            else if (!isPlaying)
            {
                var globalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this);
                if (globalObjectId.identifierType == 2)
                {
                    SceneObjectID sceneObjectID = globalObjectId.GetSceneObjectID();
                    if (globalObjectId.targetPrefabId == 0)
                    {
                        // Reset GUID to scene and object ID on regular objects
                        GUID sceneGUID = new(globalObjectId.assetGUID.ToString());
                        if (internalSceneObjectGuid != sceneGUID || internalSceneObjectId != sceneObjectID)
                        {
                            internalSceneObjectGuid = sceneGUID;
                            internalSceneObjectId = sceneObjectID;
                            UnityEditor.EditorUtility.SetDirty(this);
                        }
                    }
                    else
                    {
                        // Reset object ID on prefab instances
                        if (internalSceneObjectId != sceneObjectID)
                        {
                            internalSceneObjectId = sceneObjectID;
                            UnityEditor.EditorUtility.SetDirty(this);
                        }

                        // Clear GUID modifications on prefab instances
                        if (IsGUIDModified(this))
                        {
                            UnityEditor.SerializedObject serializedObj = new(this);
                            serializedObj.Update();
                            UnityEditor.PrefabUtility.RevertPropertyOverride(serializedObj.FindProperty(nameof(internalSceneObjectGuid)), UnityEditor.InteractionMode.AutomatedAction);
                            serializedObj.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                else if (internalSceneObjectGuid != GUID.zero || internalSceneObjectId != SceneObjectID.zero)
                {
                    // Clear everything on invalid objects
                    internalSceneObjectGuid = GUID.zero;
                    internalSceneObjectId = SceneObjectID.zero;
                    UnityEditor.EditorUtility.SetDirty(this);
                }

                // Ensure scene GUID
                Scene scene = go.scene;
                if (scene.IsValid())
                {
                    scenesPendingValidation.Add(scene);
                }
            }
        }
        private static void ValidateDelayed()
        {
            UnityEditor.EditorApplication.delayCall -= ValidateDelayed;
            bool isPlaying = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;

            // Validate objects
            scenesPendingValidation.Clear();
            for (int i = 0; i < objectsPendingValidation.Count; i++)
            {
                try
                {
                    if (objectsPendingValidation[i])
                    {
                        objectsPendingValidation[i].ValidateSceneObject(isPlaying);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            foreach (var obj in objectsPendingValidation)
            {
                obj.pendingValidation = false;
            }
            objectsPendingValidation.Clear();

            // Validate scene GUID objects
            foreach (Scene scene in scenesPendingValidation)
            {
                // Check if there already exists one
                SceneGUID sceneObject = null;
                foreach (var existingSceneObject in FindObjectsOfType<SceneGUID>())
                {
                    if (existingSceneObject.gameObject.scene == scene)
                    {
                        if (!sceneObject)
                        {
                            sceneObject = existingSceneObject;
                        }
                        else
                        {
                            Debug.LogWarning($"Destroying duplicate scene GUID object '{existingSceneObject}'");
                        }
                    }
                }
                if (!sceneObject)
                {
                    // Check if there is at least a game object with the internal name
                    var sceneGUIDType = typeof(SceneGUID);
                    string internalObjectName = sceneGUIDType.FullName;
                    foreach (var sceneGameObject in scene.GetRootGameObjects())
                    {
                        // Reuse gameobject
                        if (sceneGameObject.name == internalObjectName)
                        {
                            // Strip all current components
                            UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(sceneGameObject);
                            foreach (var component in sceneGameObject.GetComponents<Component>())
                            {
                                if (component is not Transform)
                                {
                                    DestroyImmediate(component);
                                }
                            }

                            // Add scene GUID
                            sceneObject = sceneGameObject.AddComponent<SceneGUID>();
                            UnityEditor.EditorUtility.SetDirty(sceneObject);
                            break;
                        }
                    }
                    if (!sceneObject)
                    {
                        // Game object does not exist yet, create new
                        sceneObject = UnityEditor.ObjectFactory.CreateGameObject(scene, HideFlags.HideInHierarchy, internalObjectName, sceneGUIDType).GetComponent<SceneGUID>();
                        UnityEditor.Undo.ClearUndo(sceneObject.gameObject);
                    }
                }
            }
            scenesPendingValidation.Clear();
        }
#endif
    }
}
