using AggroBird.UnityExtend;
using System;
using System.Globalization;

namespace AggroBird.SceneObjects
{
    public interface ISceneObjectReference
    {
        GUID GUID { get; set; }
        ulong ObjectID { get; set; }
        ulong PrefabId { get; set; }
    }

    // General reference (can be used to find scene objects)
    [Serializable]
    public struct SceneObjectReference : ISceneObjectReference
    {
        public SceneObjectReference(GUID guid, ulong objectId, ulong prefabId)
        {
            this.guid = guid;
            this.objectId = objectId;
            this.prefabId = prefabId;
        }
        internal SceneObjectReference(GUID guid, SceneObjectID sceneObjectID)
        {
            this.guid = guid;
            objectId = sceneObjectID.objectId;
            prefabId = sceneObjectID.prefabId;
        }

        public GUID guid;
        public ulong objectId;
        public ulong prefabId;

        GUID ISceneObjectReference.GUID { get => guid; set => guid = value; }
        ulong ISceneObjectReference.ObjectID { get => objectId; set => objectId = value; }
        ulong ISceneObjectReference.PrefabId { get => prefabId; set => prefabId = value; }

        internal readonly SceneObjectID GetSceneObjectID() => new(objectId, prefabId);

        public readonly bool HasValue() => guid != GUID.zero;

        public readonly bool Equals(SceneObjectReference other)
        {
            return guid.Equals(other.guid) && objectId.Equals(other.objectId) && prefabId.Equals(other.prefabId);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is SceneObjectReference other && Equals(other);
        }
        public override readonly int GetHashCode()
        {
            return guid.GetHashCode() ^ (objectId.GetHashCode() << 2) ^ (prefabId.GetHashCode() >> 2);
        }

        public static bool operator ==(SceneObjectReference lhs, SceneObjectReference rhs)
        {
            return lhs.Equals(rhs);
        }
        public static bool operator !=(SceneObjectReference lhs, SceneObjectReference rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override readonly string ToString()
        {
            return $"{guid.upper:x16}{guid.lower:x16}{objectId:x16}{prefabId:x16}";
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
    public struct SceneObjectReference<T> : ISceneObjectReference where T : SceneObject
    {
        public SceneObjectReference(GUID guid, ulong objectId, ulong prefabId)
        {
            this.guid = guid;
            this.objectId = objectId;
            this.prefabId = prefabId;
        }
        internal SceneObjectReference(GUID guid, SceneObjectID sceneObjectID)
        {
            this.guid = guid;
            objectId = sceneObjectID.objectId;
            prefabId = sceneObjectID.prefabId;
        }

        public GUID guid;
        public ulong objectId;
        public ulong prefabId;

        GUID ISceneObjectReference.GUID { get => guid; set => guid = value; }
        ulong ISceneObjectReference.ObjectID { get => objectId; set => objectId = value; }
        ulong ISceneObjectReference.PrefabId { get => prefabId; set => prefabId = value; }

        public static implicit operator SceneObjectReference(SceneObjectReference<T> reference) => new(reference.guid, reference.objectId, reference.prefabId);
        public readonly bool HasValue() => guid != GUID.zero;

        public override readonly string ToString()
        {
            return $"{guid.upper:x16}{guid.lower:x16}{objectId:x16}{prefabId:x16}";
        }
    }
}