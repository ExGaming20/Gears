using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using Gears.Graphics;
using Gears.World.Components;
using Gears.Utilities;

namespace Gears.World
{
    public static class SceneSerializer
    {
        private static Dictionary<string, Type>? _componentTypes;

        // Scans loaded assemblies once for every non-abstract BaseComponent
        // subtype and caches it by class name. Reflection scans are slow,
        // this must not run per component or per load.
        public static Dictionary<string, Type> ComponentTypes
        {
            get
            {
                if (_componentTypes != null) return _componentTypes;

                _componentTypes = new Dictionary<string, Type>();

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }

                    foreach (var t in types)
                    {
                        if (t.IsAbstract || t == typeof(Transform) || !typeof(BaseComponent).IsAssignableFrom(t)) continue;
                        _componentTypes[t.Name] = t;
                    }
                }

                return _componentTypes;
            }
        }

        // -----------------------------------------------------------------
        // Load (parser)
        // -----------------------------------------------------------------

        public static Scene Load(string json, string sceneName = "Scene")
        {
            var root = JsonNode.Parse(json)!.AsObject();
            string name = root["Name"]?.GetValue<string>() ?? sceneName;
            var scene = new Scene(name);

            if (root["Root"] is JsonObject rootNodes)
            {
                foreach (var kvp in rootNodes)
                {
                    var go = BuildGameObject(kvp.Key, kvp.Value!.AsObject());
                    scene.MakeGameObject(go);
                }
            }

            return scene;
        }

        private static GameObject BuildGameObject(string name, JsonObject node)
        {
            var go = new GameObject(name);

            go.Transform.LocalPosition = ReadVector3(node["Position"]);
            go.Transform.LocalEulerAngles = ReadVector3(node["Rotation"]);
            go.Transform.LocalScale = ReadVector3(node["Scale"], Vector3.One);

            if (node["Components"] is JsonObject components)
            {
                foreach (var comp in components)
                    BuildComponent(go, comp.Key, comp.Value as JsonObject);
            }

            if (node["Children"] is JsonObject children)
            {
                foreach (var child in children)
                {
                    var childGo = BuildGameObject(child.Key, child.Value!.AsObject());
                    childGo.SetParent(go);
                }
            }

            return go;
        }

        private static void BuildComponent(GameObject go, string typeName, JsonObject? fields)
        {
            if (!ComponentTypes.TryGetValue(typeName, out var type))
            {
                Logger.Instance.LogWarning($"Scene load: unknown component type '{typeName}' on '{go.Name}', skipped.");
                return;
            }

            var component = (BaseComponent)Activator.CreateInstance(type)!;

            // Fields are set before the component is attached, so Awake()/Start()
            // (fired inside GameObject.AddComponent) see real values, not defaults.
            if (fields != null)
            {
                foreach (var field in fields)
                    SetMember(component, type, field.Key, field.Value);
            }

            go.AddComponent(component);
        }

        private static void SetMember(BaseComponent component, Type type, string memberName, JsonNode? valueNode)
        {
            if (valueNode == null) return;

            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(component, ConvertValue(valueNode, prop.PropertyType));
                return;
            }

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(component, ConvertValue(valueNode, field.FieldType));
                return;
            }

            Logger.Instance.LogWarning($"Scene load: '{type.Name}' has no writable member '{memberName}', skipped.");
        }

        private static object? ConvertValue(JsonNode node, Type targetType)
        {
            if (targetType == typeof(string)) return node.GetValue<string>();
            if (targetType == typeof(bool)) return node.GetValue<bool>();
            if (targetType == typeof(int)) return ReadInt(node);
            if (targetType == typeof(float)) return ReadFloat(node);
            if (targetType == typeof(double)) return (double)ReadFloat(node);
            if (targetType.IsEnum) return Enum.ToObject(targetType, ReadInt(node));

            if (targetType == typeof(Vector2) && node is JsonObject v2)
                return new Vector2(ReadFloat(v2["X"]), ReadFloat(v2["Y"]));

            if (targetType == typeof(Vector3) && node is JsonObject v3)
                return new Vector3(ReadFloat(v3["X"]), ReadFloat(v3["Y"]), ReadFloat(v3["Z"]));

            if (targetType == typeof(Vector2i) && node is JsonObject v2i)
                return new Vector2i(ReadInt(v2i["X"]), ReadInt(v2i["Y"]));

            if (targetType == typeof(Color4) && node is JsonObject col)
                return new Color4(ReadFloat(col["R"]), ReadFloat(col["G"]), ReadFloat(col["B"]), ReadFloat(col["A"]));

            if (targetType == typeof(Mesh)) return FindLoadedAssets.FindMesh(node.GetValue<string>());
            if (targetType == typeof(Texture)) return FindLoadedAssets.FindTexture(node.GetValue<string>());
            if (targetType == typeof(Material)) return FindLoadedAssets.FindMaterial(node.GetValue<string>());

            Logger.Instance.LogWarning($"Scene load: no converter for type '{targetType.Name}', skipped.");
            return null;
        }

        private static Vector3 ReadVector3(JsonNode? node, Vector3 fallback = default)
        {
            if (node is not JsonArray arr || arr.Count < 3) return fallback;
            return new Vector3(ReadFloat(arr[0]), ReadFloat(arr[1]), ReadFloat(arr[2]));
        }

        // Accepts a JSON number or a numeric string, so old hand-written
        // scene files with quoted numbers still load.
        private static float ReadFloat(JsonNode? node)
        {
            if (node is not JsonValue val) return 0f;
            if (val.TryGetValue<float>(out var f)) return f;
            if (val.TryGetValue<string>(out var s) && float.TryParse(s, out var parsed)) return parsed;
            return 0f;
        }

        private static int ReadInt(JsonNode? node)
        {
            if (node is not JsonValue val) return 0;
            if (val.TryGetValue<int>(out var i)) return i;
            if (val.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed)) return parsed;
            return 0;
        }

        // -----------------------------------------------------------------
        // Save (constructor)
        // -----------------------------------------------------------------

        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        public static string Save(Scene scene)
        {
            var rootNodes = new JsonObject();
            foreach (var go in scene.RootObjects)
                rootNodes[go.Name] = WriteGameObject(go);

            var root = new JsonObject
            {
                ["Name"] = scene.Name,
                ["Root"] = rootNodes
            };

            return root.ToJsonString(WriteOptions);
        }

        private static JsonObject WriteGameObject(GameObject go)
        {
            return new JsonObject
            {
                ["Position"] = WriteVector3(go.Transform.LocalPosition),
                ["Rotation"] = WriteVector3(QuaternionToEulerDegrees(go.Transform.LocalRotation)),
                ["Scale"] = WriteVector3(go.Transform.LocalScale),
                ["Components"] = WriteComponents(go),
                ["Children"] = WriteChildren(go)
            };
        }

        private static JsonArray WriteVector3(Vector3 v) => new JsonArray(v.X, v.Y, v.Z);

        private static Vector3 QuaternionToEulerDegrees(Quaternion q)
        {
            Vector3 rad = q.ToEulerAngles();
            return new Vector3(
                MathHelper.RadiansToDegrees(rad.X),
                MathHelper.RadiansToDegrees(rad.Y),
                MathHelper.RadiansToDegrees(rad.Z));
        }

        private static JsonObject WriteComponents(GameObject go)
        {
            var components = new JsonObject();

            foreach (var component in go.GetComponents<BaseComponent>())
            {
                if (component is Transform) continue; // written as Position/Rotation/Scale, not a component entry
                components[component.GetType().Name] = WriteComponent(component);
            }

            return components;
        }

        private static JsonObject WriteComponent(BaseComponent component)
        {
            var type = component.GetType();
            var obj = new JsonObject();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!prop.CanRead || !prop.CanWrite) continue; // skip computed/readonly properties, they're derived, not data
                obj[prop.Name] = WriteValue(prop.GetValue(component), prop.PropertyType);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                obj[field.Name] = WriteValue(field.GetValue(component), field.FieldType);
            }

            return obj;
        }

        private static JsonNode? WriteValue(object? value, Type type)
        {
            if (value == null) return null;

            if (type == typeof(string)) return JsonValue.Create((string)value);
            if (type == typeof(bool)) return JsonValue.Create((bool)value);
            if (type == typeof(int)) return JsonValue.Create((int)value);
            if (type == typeof(float)) return JsonValue.Create((float)value);
            if (type == typeof(double)) return JsonValue.Create((double)value);
            if (type.IsEnum) return JsonValue.Create((int)value);

            if (type == typeof(Vector2)) { var v = (Vector2)value; return new JsonObject { ["X"] = v.X, ["Y"] = v.Y }; }
            if (type == typeof(Vector3)) { var v = (Vector3)value; return new JsonObject { ["X"] = v.X, ["Y"] = v.Y, ["Z"] = v.Z }; }
            if (type == typeof(Vector2i)) { var v = (Vector2i)value; return new JsonObject { ["X"] = v.X, ["Y"] = v.Y }; }
            if (type == typeof(Color4)) { var c = (Color4)value; return new JsonObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B, ["A"] = c.A }; }

            if (type == typeof(Mesh)) return ((Mesh)value).Name;
            if (type == typeof(Texture)) return ((Texture)value).Name;
            if (type == typeof(Material)) return ((Material)value).Name;

            // Unknown type: drop the single field instead of failing the whole save.
            Logger.Instance.LogWarning($"Scene save: no writer for type '{type.Name}', field dropped.");
            return null;
        }

        private static JsonObject WriteChildren(GameObject go)
        {
            var children = new JsonObject();
            foreach (var child in go.Children)
                children[child.Name] = WriteGameObject(child);
            return children;
        }
    }
}