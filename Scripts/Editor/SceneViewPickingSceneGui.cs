using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace RoyTheunissen.SceneViewPicker
{
    /// <summary>
    /// Responsible for the scene gui part of picking from the scene view.
    /// </summary>
    public static partial class SceneViewPicking
    {
        private static int controlID;
        private static SerializedProperty propertyPicking;
        private static string pickCallback;

        private const float GroupDistance = 25.0f;

        private struct Candidate
        {
            private const int IndexMaxChildren = 100;
            private const int IndexMaxDepth = 100;
            
            private readonly Transform transform;
            public Transform Transform => transform;

            private readonly Object @object;
            public Object Object => @object;

            public bool IsValid => transform != null && @object != null;
            public Vector3 Position => transform.position;
            
            private BigInteger hierarchyOrder;
            public BigInteger HierarchyOrder => hierarchyOrder;

            public string Name => transform.name;

            private GUIContent dropDownText;
            public GUIContent DropdownText => dropDownText;

            public Candidate(Object @object)
            {
                transform = GetTransform(@object);
                this.@object = @object;

                // Show the path, this helps figure out where it is in the transform hierarchy. Need to replace slash
                // with backslash otherwise it will create a separator.
                string path = GetPath(transform);
                path = path.Replace("/", "\\");
                
                string text = path + " (" + ObjectNames.NicifyVariableName(@object.GetType().Name) + ")";
                dropDownText = new GUIContent(text);

                // Determine how deep in the hierarchy this transform is.
                int hierarchyDepth = 0;
                Transform searchTransform = transform.parent;
                while (searchTransform != null)
                {
                    hierarchyDepth++;
                    searchTransform = searchTransform.parent;
                }

                hierarchyOrder = CalculateHierarchyOrder(transform, 0, hierarchyDepth);
            }

            /// <summary>
            /// This complicated looking function has a simple goal: calculating an index for a transform so that we
            /// can sort a list of candidates and be able to quickly sort them in the same way as the hierarchy view is
            /// sorted in Unity: first show ourselves, then our children, and then our siblings. Certain assumptions
            /// need to be made about how deep the hierarchy can go and how many children a transform can have.
            /// Generous limits of 100 children per transform and 100 layers deep have been used. This exceeds the
            /// capacity of integers, so that's why BigInteger has been used. If you actually have scenes with
            /// hierarchies that are this vast, please seek medical attention.
            /// </summary>
            private static BigInteger CalculateHierarchyOrder(Transform transform, BigInteger index, int hierarchyDepth)
            {
                // Assume that there is a maximum depth to layers and calculate how many are below us.
                int possibleLayersBelow = Math.Max(0, IndexMaxDepth - hierarchyDepth);
                    
                // Assuming that every transform can have a specific amount of maximum children, calculate how many
                // indices should be reserved (block size) at this layer.
                BigInteger blockSizeAtDepth = BigInteger.Pow(IndexMaxChildren, possibleLayersBelow);
                
                // We know how many siblings precede this transform, so calculate what the index is within this layer.
                BigInteger indexWithinLayer = (transform.GetSiblingIndex() + 1) * blockSizeAtDepth;

                index += indexWithinLayer;
                
                // If our recursive function has reached the root, then our index has been computed.
                if (transform.parent == null)
                    return index;

                // If there are still transforms above us, continue this function back up, recursively.
                hierarchyDepth--;
                return CalculateHierarchyOrder(transform.parent, index, hierarchyDepth);
            }

            public Vector3 GetScreenPosition(SceneView sceneView)
            {
                return sceneView.camera.WorldToScreenPoint(Position);
            }

            public bool IsBehindSceneCamera(SceneView sceneView)
            {
                return GetScreenPosition(sceneView).z < 0;
            }
            
            private static Transform GetTransform(Object @object)
            {
                // ROY: Sadly the 'transform' field is not shared by a common base class between 
                // Component and GameObject, so if we want to support both we have to check it like this. 
            
                Component component = @object as Component;
                if (component != null)
                    return component.transform;
            
                GameObject gameObject = @object as GameObject;
                if (gameObject != null)
                    return gameObject.transform;

                return null;
            }
        }

        private static List<Object> possibleCandidateObjects = new List<Object>();
        private static List<Candidate> allCandidates = new List<Candidate>();

        private static Candidate bestCandidate;
        
        private static List<Candidate> nearbyCandidates = new List<Candidate>();

        private static bool hasShownHint;

        public static SerializedProperty PropertyPicking => propertyPicking;

        private static GUIStyle cachedPickingTextStyle;
        public static GUIStyle PickingTextStyle
        {
            get
            {
                if (cachedPickingTextStyle == null)
                {
                    cachedPickingTextStyle = new GUIStyle("Box");
                    cachedPickingTextStyle.alignment = TextAnchor.MiddleCenter;
                    cachedPickingTextStyle.fontStyle = FontStyle.Bold;
                }
                
                return cachedPickingTextStyle;
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (propertyPicking == null)
            {
                StopPicking();
                return;
            }

            // Make sure we update the sceneview whenever the mouse moves.
            if (Event.current.type == EventType.MouseMove)
            {
                bestCandidate = FindBestCandidate(sceneView);
                FindNearbyCandidates(sceneView);

                sceneView.Repaint();
            }

            // Draw the current best candidate.
            if (bestCandidate.IsValid)
            {
                Vector3 objectPosWorld = bestCandidate.Position;
                Vector2 mousePosGui = Event.current.mousePosition;
                Vector3 mouseWorld = HandleUtility.GUIPointToWorldRay(mousePosGui)
                    .GetPoint(10);

                Handles.color = new Color(1, 1, 1, 0.75f);
                Handles.DrawDottedLine(objectPosWorld, mouseWorld, 2.0f);
                Handles.color = Color.white;

                Handles.BeginGUI();

                string text = bestCandidate.Name;
                
                // The 'nearby candidates' includes the best candidate, if there's more than one, there are others.
                if (nearbyCandidates.Count > 1)
                    text += " + " + (nearbyCandidates.Count - 1) + " nearby";

                Vector2 labelSize = PickingTextStyle.CalcSize(new GUIContent(text));
                labelSize += Vector2.one * 4;
                Rect nameRect = new Rect(
                    Event.current.mousePosition + Vector2.down * 10 - labelSize * 0.5f, labelSize);

                // Draw shadow.
                GUI.backgroundColor = new Color(0, 0, 0, 1.0f);
                PickingTextStyle.normal.textColor = Color.black;
                EditorGUI.LabelField(nameRect, text, PickingTextStyle);

                // Draw white text.
                nameRect.position += new Vector2(-1, -1);
                GUI.backgroundColor = new Color(0, 0, 0, 0);
                PickingTextStyle.normal.textColor = Color.white;
                EditorGUI.LabelField(nameRect, text, PickingTextStyle);

                Handles.EndGUI();
            }

            // This makes sure that clicks are not handled by the scene itself.
            if (Event.current.type == EventType.Layout)
            {
                controlID = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(controlID);
                return;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.alt ||
                Event.current.control)
            {
                return;
            }

            // Left click to pick a candidate, every other button is a cancel.
            if (Event.current.button == 0)
            {
                PickCandidate(bestCandidate);
            }
            else if (Event.current.button == 2 && nearbyCandidates.Count > 1)
            {
                PickNearbyCandidate();
            }
            else
            {
                StopPicking();
            }

            Event.current.Use();
        }

        private static void PickNearbyCandidate()
        {
            GenericMenu menu = new GenericMenu();
            menu.allowDuplicateNames = true;

            for (int i = 0; i < nearbyCandidates.Count; i++)
            {
                // Declare this so it is referenced correctly in the anonymous method passed to the menu.
                Candidate candidate = nearbyCandidates[i];
                
                menu.AddItem(candidate.DropdownText, false, () => PickCandidate(candidate));
            }
            
            menu.ShowAsContext();
        }

        private static void PickCandidate(Candidate candidate)
        {
            if (propertyPicking == null || !candidate.IsValid)
                return;

            // Actually apply the value.
            propertyPicking.serializedObject.Update();

            Object previousValue = propertyPicking.objectReferenceValue;
            Object currentValue = candidate.Object;

            propertyPicking.objectReferenceValue = currentValue;
            propertyPicking.serializedObject.ApplyModifiedProperties();

            FireSceneViewPickerCallback(previousValue, currentValue);

            StopPicking();
        }

        public static void FireSceneViewPickerCallback(Object previousValue, Object currentValue)
        {
            FireSceneViewPickerCallback(propertyPicking, pickCallback, previousValue, currentValue);
        }

        public static void FireSceneViewPickerCallback(
            SerializedProperty property, string callback, Object previousValue, Object currentValue)
        {
            if (property == null || string.IsNullOrEmpty(callback))
                return;

            object target = GetParentObject(property);
            MethodInfo method = GetMethodIncludingFromBaseClasses(target.GetType(), callback);
            if (method == null)
            {
                Debug.LogWarningFormat(
                    "Was asked to fire callback '{0}' but object '{1}' " +
                    "did not seem to have one. Path is {2}.", callback,
                    target, property.propertyPath);
                return;
            }

            ParameterInfo[] parameters = method.GetParameters();

            // If it has 2 parameters, invoke it with the previous and current value.
            if (parameters.Length == 2)
            {
                object previous = Cast(previousValue, parameters[0].ParameterType);
                object current = Cast(currentValue, parameters[1].ParameterType);
                method.Invoke(target, new[] {previous, current});
                return;
            }

            // Parameterless callback, just fire it right now.
            method.Invoke(target, new object[0]);
        }

        private static float GetDistanceToMouse(Candidate candidate, SceneView sceneView)
        {
            // Figure out where the object is relative to the scene view camera.
            Vector3 positionScreen = candidate.GetScreenPosition(sceneView);

            // Let the Z distance count for prioritization, but not as much as X and Y.
            positionScreen.z /= 10;

            Vector3 mouseScreen = Event.current.mousePosition;
            mouseScreen.y = sceneView.position.height - mouseScreen.y;
            
            return Vector3.Distance(mouseScreen, positionScreen);
        }
        
        private static float GetDistance(Candidate candidate1, Candidate candidate2, SceneView sceneView)
        {
            // Figure out where the object is relative to the scene view camera.
            Vector3 positionScreen1 = candidate1.GetScreenPosition(sceneView);
            Vector3 positionScreen2 = candidate2.GetScreenPosition(sceneView);

            // Let the Z distance count for prioritization, but not as much as X and Y.
            positionScreen1.z /= 10;
            positionScreen2.z /= 10;

            return Vector3.Distance(positionScreen1, positionScreen2);
        }

        private static Candidate FindBestCandidate(SceneView sceneView)
        {
            float distanceMin = float.PositiveInfinity;
            Candidate bestCandidate = default(Candidate);

            foreach (Candidate candidate in allCandidates)
            {
                if (candidate.IsBehindSceneCamera(sceneView))
                    continue;
                
                float distance = GetDistanceToMouse(candidate, sceneView);
                
                // Find the closest one.
                if (distance < distanceMin)
                {
                    bestCandidate = candidate;
                    distanceMin = distance;
                }
            }
            
            return bestCandidate;
        }
        
        private static void FindNearbyCandidates(SceneView sceneView)
        {
            nearbyCandidates.Clear();
            
            if (!bestCandidate.IsValid)
                return;

            foreach (Candidate candidate in allCandidates)
            {
                if (candidate.IsBehindSceneCamera(sceneView))
                    continue;
                
                // Find any candidates that are very close to the best candidate.
                float distance = GetDistance(bestCandidate, candidate, sceneView);
                if (distance < GroupDistance)
                    nearbyCandidates.Add(candidate);
            }
            
            nearbyCandidates.Sort(SortNearbyCandidates);
        }

        private static int SortNearbyCandidates(Candidate x, Candidate y)
        {
            float distanceXToBestCandidate = Vector3.Distance(x.Position, bestCandidate.Position);
            float distanceYToBestCandidate = Vector3.Distance(y.Position, bestCandidate.Position);

            int comparison = distanceXToBestCandidate.CompareTo(distanceYToBestCandidate);
            
            if (comparison != 0)
                return comparison;
            
            // If they are on the same transform, sort alphabetically...
            if (x.Transform == y.Transform)
                return x.Name.CompareTo(y.Name);
            
            // If they are at the same distance to the candidate, go by hierarchy order instead. This will group
            // transforms by their children and respect the sibling order too.
            return x.HierarchyOrder.CompareTo(y.HierarchyOrder);
        }

        public static void StartPicking(SerializedProperty property, Type type, string callback)
        {
            if (!hasShownHint)
            {
                SceneView.lastActiveSceneView.ShowNotification(
                    new GUIContent(
                        "Left click: Pick an object in scene \n\n" +
                        "Middle click: Choose from nearby objects \n\n" +
                        "Right click: Cancel"), 3);
                hasShownHint = true;
            }

            propertyPicking = property;
            pickCallback = callback;
            
            FindAllCandidates(type);

            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            SceneView.lastActiveSceneView.Repaint();
        }

        private static Scene GetScene(Object @object)
        {
            if (@object is Component component)
                return component.gameObject.scene;

            if (@object is GameObject gameObject)
                return gameObject.scene;

            return default(Scene);
        }
        
        private static void FindObjectsOfTypeInSceneOrPrefab(Type type, ref List<Object> @objects)
        {
            Scene currentScene = GetScene(propertyPicking.serializedObject.targetObject);

            // If there is a valid scene, use that scene to find candidates instead.
            objects.Clear();
            if (currentScene.IsValid())
            {
                GameObject[] rootGameObjects = currentScene.GetRootGameObjects();
                for (int i = 0; i < rootGameObjects.Length; i++)
                    objects.AddRange(rootGameObjects[i].GetComponentsInChildren(type));
            }
            else
            {
                objects.AddRange(Object.FindObjectsOfType(type));
            }

            // Filter out components belonging to the wrong scene.
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (GetScene(objects[i]) != currentScene)
                    objects.RemoveAt(i);
            }
        }

        private static void FindGameObjectsInSceneOrPrefab(ref List<Object> @objects)
        {
            Scene currentScene = GetScene(propertyPicking.serializedObject.targetObject);

            // If there is a valid scene, use that scene to find candidates instead.
            objects.Clear();
            if (currentScene.IsValid())
            {
                GameObject[] rootGameObjects = currentScene.GetRootGameObjects();
                for (int i = 0; i < rootGameObjects.Length; i++)
                {
                    Transform[] transforms = rootGameObjects[i].GetComponentsInChildren<Transform>();
                    for (int j = 0; j < transforms.Length; j++)
                    {
                        Transform transform = transforms[j];
                        objects.Add(transform.gameObject);
                    }
                }
            }
            else
            {
                objects.AddRange(Object.FindObjectsOfType(typeof(GameObject)));
            }

            // Filter out components belonging to the wrong scene.
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (GetScene(objects[i]) != currentScene)
                    objects.RemoveAt(i);
            }
        }
        
        private static void FindAllCandidates(Type type)
        {
            bool isInterface = type.IsInterface;
            
            possibleCandidateObjects.Clear();
            if (isInterface)
            {
                // If the type is an interface type then we can't directly search for all instances so we search for all
                // MonoBehaviours and filter out the wrong types here.
                FindObjectsOfTypeInSceneOrPrefab(typeof(MonoBehaviour), ref possibleCandidateObjects);
                
                for (int i = possibleCandidateObjects.Count - 1; i >= 0; i--)
                {
                    if (isInterface && !type.IsInstanceOfType(possibleCandidateObjects[i]))
                        possibleCandidateObjects.RemoveAt(i);
                }
            }
            else if (type == typeof(GameObject))
            {
                FindGameObjectsInSceneOrPrefab(ref possibleCandidateObjects);
            }
            else
            {
                FindObjectsOfTypeInSceneOrPrefab(type, ref possibleCandidateObjects);
            }
            
            allCandidates.Clear();
            for (int i = 0; i < possibleCandidateObjects.Count; i++)
                allCandidates.Add(new Candidate(possibleCandidateObjects[i]));
        }

        public static void StopPicking()
        {
            allCandidates.Clear();
            bestCandidate = default(Candidate);

            propertyPicking = null;
            pickCallback = null;

            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        // This is stuff that I copied to make scene picking standalone...
        #region Extensions
        public static string GetPath(Transform transform, Transform relativeTo = null)
        {
            if (relativeTo != null && !transform.IsChildOf(relativeTo))
            {
                Debug.LogWarningFormat(
                    transform,
                    "Tried to get path of transform {0} relative to transform {1}, " +
                    "which isn't actually a parent of it.",
                    transform, relativeTo);
                return null;
            }

            string path = transform.name;
            
            Transform current = transform.parent;
            while (current != relativeTo)
            {
                path = current.name + "/" + path;
                
                current = current.parent;
            }

            return path;
        }
        
        public static object GetParentObject(SerializedProperty property)
        {
            string path = property.propertyPath;
            int indexOfLastSeparator = path.LastIndexOf(".", StringComparison.Ordinal);

            // No separators means it's a root object and there's no parent.
            if (indexOfLastSeparator == -1)
                return property.serializedObject.targetObject;

            string pathExcludingLastObject = path.Substring(0, indexOfLastSeparator);
            return GetActualObjectByPath(property.serializedObject, pathExcludingLastObject);
        }
        
        public static object GetActualObjectByPath(SerializedObject serializedObject, string path)
        {
            return GetActualObjectByPath(serializedObject.targetObject, path);
        }

        public static object GetActualObjectByPath(Object owner, string path)
        {
            // Sample paths:    connections.Array.data[0].to
            //                  connection.to
            //                  to

            string[] pathSections = path.Split('.');

            object value = owner;
            for (int i = 0; i < pathSections.Length; i++)
            {
                Type valueType = value.GetType();

                if (valueType.IsArray)
                {
                    // Parse the next section which contains the index. 
                    string indexPathSection = pathSections[i + 1];
                    indexPathSection = Regex.Replace(indexPathSection, @"\D", "");
                    int index = int.Parse(indexPathSection);

                    // Get the value from the array.
                    Array array = value as Array;
                    value = array.GetValue(index);
                    
                    // We can now skip the next section which is the one with the index.
                    i++;
                    continue;
                }
                
                // Go deeper down the hierarchy by searching in the current value for a field with
                // the same name as the current path section and then getting that value.
                FieldInfo fieldInfo = valueType.GetField(
                    pathSections[i], BindingFlags.Instance | BindingFlags.NonPublic);
                value = fieldInfo.GetValue(value);
            }

            return value;
        }
        
        public static MethodInfo GetMethodIncludingFromBaseClasses(Type type, string name)
        {
            MethodInfo methodInfo = null;
            Type baseType = type;
            while (methodInfo == null)
            {
                methodInfo = baseType.GetMethod(
                    name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (methodInfo != null)
                    return methodInfo;
                
                baseType = baseType.BaseType;
                if (baseType == null)
                    break;
            }

            return null;
        }
        
        private static MethodInfo castSafeMethod;
    
        public static object Cast(object data, Type type)
        {
            if (data == null)
                return null;

            // ROY: Contrary to Convert.ChangeType, this one will also work when casting a derived type
            // to one of its base classes.
        
            if (castSafeMethod == null)
            {
                castSafeMethod = typeof(SceneViewPickerPropertyDrawer).GetMethod(
                    "CastStronglyTyped", BindingFlags.NonPublic | BindingFlags.Static);
            }

            MethodInfo castSafeMethodGeneric = castSafeMethod.MakeGenericMethod(type);

            return castSafeMethodGeneric.Invoke(null, new[] {data});
        }
        
        private static T CastStronglyTyped<T>(object data)
        {
            return (T)data;
        }
        #endregion Extensions
    }
}
