using AggroBird.UnityExtend.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace AggroBird.SceneObjects.Editor
{
    public static class SceneObjectPropertyUtility
    {
        public static SceneObjectReference GetSceneObjectReferenceValue(this SerializedProperty property)
        {
            if (property == null)
            {
                throw new NullReferenceException(nameof(property));
            }
            if (property.type != typeof(SceneObjectReference).FullName)
            {
                Debug.LogError($"Property is not a {typeof(SceneObjectReference).Name}");
                return default;
            }
            var guid = property.FindPropertyRelative(nameof(SceneObjectReference.guid)).GetGUIDValue();
            var objectId = property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue;
            var prefabId = property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue;
            return new(guid, objectId, prefabId);
        }
        public static void SetSceneObjectReferenceValue(this SerializedProperty property, SceneObjectReference sceneObjectReference)
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
            property.FindPropertyRelative(nameof(SceneObjectReference.guid)).SetGUIDValues(sceneObjectReference.guid);
            property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue = sceneObjectReference.objectId;
            property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue = sceneObjectReference.prefabId;
        }
    }
}