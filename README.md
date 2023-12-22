# SceneObjects
Utility plugin that expands on [GlobalObjectId](https://docs.unity3d.com/ScriptReference/GlobalObjectId.html) to allow serializing scene object references in assets and prefabs.

The declaration for a serialized scene reference looks like this:

```csharp
public SceneObjectReference<SomeObject> sceneReference;
```

Scene objects need to derive from the `SceneObject` monobehaviour.

## Example

When inspecting an object that has scene reference properties, the properties look like regular object fields, with the addition of small buttons that indicate whether the reference is a scene object (scene icon) or a prefab (prefab icon).

![alt text](https://github.com/AggroBird/SceneObjects/blob/main/Documentation~/insideSceneExample.png?raw=true "Inside scene example")

When the reference points to a scene object and the scene is not currently loaded in the editor, the property will show this in the field. Pressing the button will open the scene that the object is located in, or in the case of a prefab, open the prefab asset.

![alt text](https://github.com/AggroBird/SceneObjects/blob/main/Documentation~/outsideSceneExample.png?raw=true "Inside scene example")

## Implementation

The `SceneObject` component reads out its own GUID and object ID when placed in a scene, and saves it in OnValidate(). These need to be serialized because they are lost when the scene has started playing. This operation dirties the scene state, but only when the GUID has been changed. Under normal circumstances, this should only happen when a new scene object is created or duplicated. These GUID's and ID's are guaranteed to be persistent, since Unity uses them internally to keep object references in serialization.

Internally, the references inside the property are serialized as a GUID and an object ID. In the case of a prefab, the GUID is that of the prefab asset. In the case of a scene object, the GUID is that of the scene, where the Object ID is used to identify the object within the scene. Prefab references will have an Object ID of 0.

## Usage

Scene objects can only be found when the scene is playing. Scene objects register themselves internally in `SceneObject.Awake()`. After registration they can be found through `SceneObject.FindSceneObjects<T>()`. When using a prefab reference, the function will return all prefab instances in the scene of the same prefab type. When using a reference that points to a scene object (that has an Object ID), the function will return that specific object only. When searching for one particular object, `SceneObject.TryFindSceneObject<T>()` is faster.

The scene object reference value to an object in the scene can be retrieved with the `SceneObject.GetReference()` member function, and can be used for retrieving the same object again later. This can only be done when the scene is playing, after the objects have been registered. Do not use the GUID from the inspector directly since the GUID stored inside the object may be that of a prefab asset.

## Notice

Care must be taken to preserve links to scene objects. Deleting an object in a scene will break any references in a similar fashion to regular serialized objects. Objects instantiated in the scene (either from a source object or a prefab) will get a new Object ID assigned. There is no guarantee these ID's will be the same, as they are dependend on the order of instantiation. Instantiating any scene objects before all pre-placed scene object registration is completed may cause collisions in the lookup.