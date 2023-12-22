using AggroBird.UnityExtend;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("AggroBird.SceneObjects.Editor")]

namespace AggroBird.SceneObjects
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal sealed class SceneObjectIDAttribute : PropertyAttribute
    {

    }

    public abstract class SceneObject : MonoBehaviour
    {
        // In the case of a regular scene object, this contains the scene GUID
        // In the case of a scene prefab instance, this contains the prefab GUID
        [SerializeField] internal GUID guid;
        // In case of an actual prefab, this will be 0
        [SerializeField, SceneObjectID] internal ulong objectId;

        // The GUID of the scene this object is in at play time (zero if registration failed)
        private GUID sceneGUID;

        // Check if this object is referenced by the provided reference
        // (Either its part of the same prefab family, or it is the actual scene object pointed to)
        public bool IsReferenced(SceneObjectReference reference)
        {
            if (reference.guid != GUID.zero && sceneGUID != GUID.zero)
            {
                if (reference.objectId == 0)
                {
                    // Prefab type comparison
                    return reference.guid == guid;
                }
                else
                {
                    // Specific scene object comparison, ensure its a object within this scene
                    // (Don't use the object's GUID, it might be a prefab)
                    if (reference.guid == sceneGUID)
                    {
                        return reference.objectId == objectId;
                    }
                }
            }

            return false;
        }

        // Get a reference for later identification
        // Returns an invalid reference if the scene is not playing or if the object is an uninstantiated prefab
        public SceneObjectReference GetReference()
        {
            if (sceneGUID == GUID.zero || objectId == 0)
            {
                return default;
            }

            return new(sceneGUID, objectId);
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
            if (reference)
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
            if (reference)
            {
                List<T> list = ListBuffer<T>.Get();
                foreach (var sceneGUIDObj in SceneGUID.AllScenes)
                {
                    sceneGUIDObj.FindSceneObjects(reference, list);
                }
                return list.ToArray();
            }
            return Array.Empty<T>();
        }
        public static T[] FindSceneObjects<T>(SceneObjectReference<T> reference) where T : SceneObject
        {
            return FindSceneObjects<T>((SceneObjectReference)reference);
        }

        // Find all scene objects that match reference within a specific scene
        public static T[] FindSceneObjects<T>(Scene scene, SceneObjectReference reference) where T : SceneObject
        {
            if (SceneGUID.TryGetSceneGUIDObject(scene, out var sceneGUIDObj))
            {
                List<T> list = ListBuffer<T>.Get();
                sceneGUIDObj.FindSceneObjects(reference, list);
                return list.ToArray();
            }
            return Array.Empty<T>();
        }
        public static T[] FindSceneObjects<T>(Scene scene, SceneObjectReference<T> reference) where T : SceneObject
        {
            return FindSceneObjects<T>(scene, (SceneObjectReference)reference);
        }


        protected virtual void Awake()
        {
            sceneGUID = SceneGUID.RegisterSceneObject(this);
        }

#if UNITY_EDITOR
        private static readonly string GUIDUpperPropertyPath = $"{nameof(guid)}.{Utility.GetPropertyBackingFieldName("Upper")}";
        private static readonly string GUIDLowerPropertyPath = $"{nameof(guid)}.{Utility.GetPropertyBackingFieldName("Lower")}";
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
                var globalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(go);
                if (globalObjectId.identifierType == 1)
                {
                    // Reset GUID and clear object ID on original prefabs
                    GUID assetGUID = new(globalObjectId.assetGUID.ToString());
                    if (assetGUID != guid || objectId != 0)
                    {
                        guid = assetGUID;
                        objectId = 0;
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
                else if (guid != GUID.zero || objectId != 0)
                {
                    // Clear everything on invalid objects
                    guid = GUID.zero;
                    objectId = 0;
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
            else if (!isPlaying)
            {
                var globalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this);
                if (globalObjectId.identifierType == 2)
                {
                    if (globalObjectId.targetPrefabId == 0)
                    {
                        // Reset GUID and object ID on regular objects
                        GUID sceneGUID = new(globalObjectId.assetGUID.ToString());
                        if (guid != sceneGUID || objectId != globalObjectId.targetObjectId)
                        {
                            guid = sceneGUID;
                            objectId = globalObjectId.targetObjectId;
                            UnityEditor.EditorUtility.SetDirty(this);
                        }
                    }
                    else
                    {
                        // Reset object ID on prefab instances
                        if (objectId != globalObjectId.targetPrefabId)
                        {
                            objectId = globalObjectId.targetPrefabId;
                            UnityEditor.EditorUtility.SetDirty(this);
                        }

                        // Clear GUID modifications on prefab instances
                        if (IsGUIDModified(this))
                        {
                            UnityEditor.SerializedObject serializedObj = new(this);
                            serializedObj.Update();
                            UnityEditor.PrefabUtility.RevertPropertyOverride(serializedObj.FindProperty(nameof(guid)), UnityEditor.InteractionMode.AutomatedAction);
                            serializedObj.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                else if (guid != GUID.zero || objectId != 0)
                {
                    // Clear everything on invalid objects
                    guid = GUID.zero;
                    objectId = 0;
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
        protected virtual void OnValidate()
        {
            if (!pendingValidation)
            {
                if (objectsPendingValidation.Count == 0)
                {
                    UnityEditor.EditorApplication.delayCall += ValidateDelayed;
                }
                objectsPendingValidation.Add(this);
                pendingValidation = true;
            }
        }
#endif
    }
}