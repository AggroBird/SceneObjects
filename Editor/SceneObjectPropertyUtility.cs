using AggroBird.UnityExtend.Editor;
using System;
using UnityEditor;
using UnityEngine;
using GUID = AggroBird.UnityExtend.GUID;

namespace AggroBird.SceneObjects.Editor
{
    public static class SceneObjectPropertyUtility
    {
        public static void GetSceneObjectReferenceValues(this SerializedProperty property, out GUID guid, out ulong objectId, out ulong prefabId)
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
        public static void SetSceneObjectReferenceValues(this SerializedProperty property, GUID guid, ulong objectId, ulong prefabId)
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
            property.FindPropertyRelative(nameof(SceneObjectReference.guid)).SetGUIDValues(guid);
            property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue = objectId;
            property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue = prefabId;
        }

        public static SceneObjectReference<SceneObject> GetSceneObjectReferenceValue(this SerializedProperty property)
        {
            try
            {
                GetSceneObjectReferenceValues(property, out GUID guid, out ulong objectId, out ulong prefabId);
                return new SceneObjectReference<SceneObject>(guid, objectId, prefabId);
            }
            catch
            {
                Debug.LogError("type is not a scene object reference value");
                return default;
            }
        }
    }
}