# SceneObjects
Utility plugin that expands on [GlobalObjectId](https://docs.unity3d.com/ScriptReference/GlobalObjectId.html) to allow serializing scene object references in assets and prefabs.

The declaration for a serialized scene reference looks like this:

```csharp
public SceneObjectReference<SomeObject> sceneReference;
```

Scene objects need to derive from the `SceneObject` monobehaviour. Internally it saves its GUID and Object ID in OnValidate().

When inspecting an object that has scene reference properties, the properties look like regular object fields, with the addition of small buttons that indicate whether the reference is a scene object (scene icon) or a prefab (prefab icon).

![alt text](https://github.com/AggroBird/SceneObjects/blob/main/Documentation~/insideSceneExample.png?raw=true "Inside scene example")

When the reference points to a scene object and the scene is not currently loaded in the editor, the property will show this. Pressing the button will open the scene that the object is located in, or in the case of a prefab, open the prefab asset.

![alt text](https://github.com/AggroBird/SceneObjects/blob/main/Documentation~/outsideSceneExample.png?raw=true "Inside scene example")

Internally, the references are serialized as a GUID and an object ID. In the case of a prefab, the GUID is that of the prefab asset. In the case of a scene object, the GUID is that of the scene, where the Object ID is used to identify the object within the scene. Prefab references will have an Object ID of 0.

Scene objects can only be found when the scene is playing. Scene objects register themselves internally in SceneObject.Awake(). Instantiating any scene objects before all registration is finished may cause collisions in the looku. After registration they can be found through `T[] SceneObject.FindSceneObjects<T>()`. When using a prefab reference, the function will return all prefab instances in the scene of the same prefab type. When using a reference that points to a scene object (that has an Object ID), the function will return that specific object only. When searching for one particular object, `bool SceneObject.TryFindSceneObject<T>()` is faster.

The scene object reference to an object in the scene can be retrieved with the `SceneObjectReference GetReference()` member function. This can only be done when the scene is playing, after the objects have been registered.

Care must be taken to preserve links to scene objects. Deleting an object in the scene will break the reference in a similar fashion to regular serialized objects.