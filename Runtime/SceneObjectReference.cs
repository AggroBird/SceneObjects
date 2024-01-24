using AggroBird.UnityExtend;
using System;
using System.Globalization;
using UnityEngine;

namespace AggroBird.SceneObjects.Editor
{
#if UNITY_EDITOR
    public static class SceneObjectEditorUtility
    {
        internal static SceneObjectID GetSceneObjectID(this UnityEditor.GlobalObjectId globalObjectId)
        {
            return new(globalObjectId.targetObjectId, globalObjectId.targetPrefabId);
        }

        internal static Func<GUID, ulong, ulong, Type, (bool found, SceneObject obj)> tryResolveSceneObjectReferenceInternal;

        // Editor only utility function for finding references outside of play time
        // If the return value is true, but the result is null, the object is located in another scene
        public static bool TryResolveSceneObjectReference<T>(SceneObjectReference<T> reference, out T result) where T : SceneObject
        {
            if (reference && tryResolveSceneObjectReferenceInternal != null)
            {
                (bool found, SceneObject obj) = tryResolveSceneObjectReferenceInternal(reference.guid, reference.objectId, reference.prefabId, typeof(T));
                if (found)
                {
                    result = obj as T;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
#endif
}

namespace AggroBird.SceneObjects
{
    // General reference (can be used to find scene objects)
    public readonly struct SceneObjectReference
    {
        public SceneObjectReference(GUID guid, ulong objectId, ulong prefabId)
        {
            this.guid = guid;
            this.objectId = objectId;
            this.prefabId = prefabId;
        }

        internal readonly GUID guid;
        internal readonly ulong objectId;
        internal readonly ulong prefabId;

        internal SceneObjectID GetSceneObjectID() => new(objectId, prefabId);

        public static implicit operator bool(SceneObjectReference reference) => reference.guid != GUID.zero;

        public bool Equals(SceneObjectReference other)
        {
            return guid.Equals(other.guid) && objectId.Equals(other.objectId) && prefabId.Equals(other.prefabId);
        }

        public override bool Equals(object obj)
        {
            return obj is SceneObjectReference other && Equals(other);
        }
        public override int GetHashCode()
        {
            return guid.GetHashCode() ^ (objectId.GetHashCode() << 2) ^ (prefabId.GetHashCode() >> 2);
        }

        public override string ToString()
        {
            return $"{guid.Upper:x16}{guid.Lower:x16}{objectId:x16}{prefabId:x16}";
        }
        public static bool TryParse(string str, out SceneObjectReference reference)
        {
            if (str != null && str.Length == 64)
            {
                if (ulong.TryParse(str.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong upper))
                {
                    if (ulong.TryParse(str.AsSpan(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong lower))
                    {
                        if (ulong.TryParse(str.AsSpan(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong objectId))
                        {
                            if (ulong.TryParse(str.AsSpan(48, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong prefabId))
                            {
                                reference = new SceneObjectReference(new GUID(upper, lower), objectId, prefabId);
                                return true;
                            }
                        }
                    }
                }
            }
            reference = default;
            return false;
        }
    }

    // Property field (can only be assigned in inspector)
    [Serializable]
    public struct SceneObjectReference<T> where T : SceneObject
    {
#if UNITY_EDITOR
        internal SceneObjectReference(GUID guid, ulong objectId, ulong prefabId)
        {
            this.guid = guid;
            this.objectId = objectId;
            this.prefabId = prefabId;
        }
#endif

        [SerializeField] internal GUID guid;
        [SerializeField] internal ulong objectId;
        [SerializeField] internal ulong prefabId;

        public static implicit operator SceneObjectReference(SceneObjectReference<T> reference) => new(reference.guid, reference.objectId, reference.prefabId);
        public static implicit operator bool(SceneObjectReference<T> reference) => reference.guid != GUID.zero;
    }
}