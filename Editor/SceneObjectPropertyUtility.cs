using AggroBird.UnityExtend.Editor;
using System;
using UnityEditor;
using UnityEngine;
using GUID = AggroBird.UnityExtend.GUID;

namespace AggroBird.SceneObjects.Editor
{
    public static class SceneObjectPropertyUtility
    {
        public static void GetSceneObjectReferenceValue(this SerializedProperty property, out GUID guid, out ulong objectId, out ulong prefabId)
        {
            if (property == null)
            {
                throw new NullReferenceException(nameof(property));
            }
            if (property.type != typeof(SceneObjectReference).FullName)
            {
                Debug.LogError($"Property is not a {typeof(SceneObjectReference).Name}");
                guid = default;
                objectId = default;
                prefabId = default;
                return;
            }
            guid = property.FindPropertyRelative(nameof(SceneObjectReference.guid)).GetGUIDValue();
            objectId = property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue;
            prefabId = property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue;
        }
        public static void SetSceneObjectReferenceValue(this SerializedProperty property, GUID guid, ulong objectId, ulong prefabId)
        {
            if (property == null)
            {
                throw new NullReferenceException(nameof(property));
            }
            if (property.type != typeof(SceneObjectReference).FullName)
            {
                Debug.LogError($"Property is not a {typeof(SceneObjectReference).Name}");
                return;
            }
            property.FindPropertyRelative(nameof(SceneObjectReference.guid)).SetGUIDValue(guid);
            property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue = objectId;
            property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue = prefabId;
        }

        public static SceneObjectReference<SceneObject> GetSceneObjectReferenceValue(this SerializedProperty property)
        {
            GetSceneObjectReferenceValue(property, out var guid, out var objectId, out var prefabId);
            return new(guid, objectId, prefabId);
        }
        public static void SetSceneObjectReferenceValue(this SerializedProperty property, SceneObjectReference sceneObjectReference)
        {
            SetSceneObjectReferenceValue(property, sceneObjectReference.guid, sceneObjectReference.objectId, sceneObjectReference.prefabId);
        }
    }
}