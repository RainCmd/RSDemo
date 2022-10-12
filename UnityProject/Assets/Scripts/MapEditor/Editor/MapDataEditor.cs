using UnityEngine;
using UnityEditor;
using System.IO;
using System.Security.Policy;

namespace NameSpace
{
    [CustomEditor(typeof(MapData))]
    public class MapDataEditor : Editor
    {
        private long width, height, brush;
        private bool editMode = true;
        private MapData data;
        private Vector2 sp;
        private void OnEnable()
        {
            data = (MapData)target;
            data.Show(true);
            width = data.map.width;
            height = data.map.height;
        }
        private void OnDisable()
        {
            data.Show(false);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sprites"), true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 40;
            width = EditorGUILayout.LongField("宽", width);
            height = EditorGUILayout.LongField("高", height);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("创建Map"))
            {
                data.Init(width, height);
            }
            if (GUILayout.Button("Clear"))
            {
                data.Clear();
            }
            EditorGUILayout.EndHorizontal();
            editMode = EditorGUILayout.Foldout(editMode, "编辑");
            if (editMode)
            {
                EditorGUILayout.BeginVertical(EditorStyles.textArea);
                sp = EditorGUILayout.BeginScrollView(sp, GUILayout.MaxHeight(320));
                var os = new GUILayoutOption[] { GUILayout.Width(180), GUILayout.Height(30) };
                if (data.sprites == null || data.sprites.Length == 0)
                {
                    data.sprites = Resources.LoadAll<Sprite>("map");
                }
                for (int i = 0; i < data.sprites.Length; i++)
                {
                    var rect = EditorGUILayout.GetControlRect(os);
                    rect.width /= 3;
                    if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                    {
                        brush = i;
                        Event.current.Use();
                        Repaint();
                    }
                    EditorGUI.ObjectField(rect, data.sprites[i], typeof(Sprite), true);
                    if (++i >= data.sprites.Length) break;
                    rect.x += rect.width;
                    if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                    {
                        brush = i;
                        Event.current.Use();
                        Repaint();
                    }
                    EditorGUI.ObjectField(rect, data.sprites[i], typeof(Sprite), true);
                    if (++i >= data.sprites.Length) break;
                    rect.x += rect.width;
                    if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                    {
                        brush = i;
                        Event.current.Use();
                        Repaint();
                    }
                    EditorGUI.ObjectField(rect, data.sprites[i], typeof(Sprite), true);
                }
                EditorGUILayout.EndScrollView();
                brush = EditorGUILayout.LongField("刷子", brush);
                EditorGUILayout.BeginHorizontal();
                if (brush >= 0 && brush < data.sprites.Length)
                {
                    var sprite = data.sprites[brush];
                    EditorGUILayout.ObjectField(sprite, typeof(Sprite), true, GUILayout.Width(120), GUILayout.Height(60));
                }
                if (GUILayout.Button("地板上加上装饰"))
                {
                    for (int x = 0; x < data.map.width; x++)
                    {
                        for (int y = 0; y < data.map.height; y++)
                        {
                            if (data.map[x, y] == 2 && data[x, y + 1])
                            {
                                data.SetMap(x, y + 1, 1);
                            }
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Save"))
            {
                var buf = data.map.Ser();
                var path = EditorUtility.SaveFilePanelInProject("保存地图数据", "mapData", "bytes", "");
                if (!string.IsNullOrEmpty(path))
                {
                    using (var fs = File.Create(Application.dataPath.Replace("Assets", path)))
                    {
                        fs.Write(buf, 0, buf.Length);
                    }
                    AssetDatabase.Refresh();
                }
            }
            var ta = (TextAsset)EditorGUILayout.ObjectField("加载地图", null, typeof(TextAsset), true);
            if (ta)
            {
                data.LoadMap(Map.Des(ta.bytes));
            }
        }
        private void OnSceneGUI()
        {
            if (editMode)
            {
                SceneView.RepaintAll();
                var pos = Event.current.mousePosition;
                pos.y = SceneView.currentDrawingSceneView.position.height - pos.y;
                var ray = SceneView.currentDrawingSceneView.camera.ScreenPointToRay(pos * 1.25f);
                var p = ray.origin;
                Handles.DrawLine(new Vector2(p.x + .25f, p.y), new Vector2(p.x - .25f, p.y));
                Handles.DrawLine(new Vector2(p.x, p.y + .25f), new Vector2(p.x, p.y - .25f));
                var x = Mathf.RoundToInt(p.x / 2);
                var y = Mathf.RoundToInt(p.y);
                var trg = data[x, y];
                if (trg)
                {
                    GUILayout.Label(string.Format("{0},{1}", x, y));
                    Handles.color = Event.current.alt ? Color.cyan : Color.green;
                    Handles.DrawLine(new Vector2(x * 2 - 1, y - .5f), new Vector2(x * 2 + 1, y + .5f));
                    Handles.DrawLine(new Vector2(x * 2 + 1, y - .5f), new Vector2(x * 2 - 1, y + .5f));
                    if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
                    {
                        if (Event.current.alt)
                        {
                            brush = data.map[x, y];
                            Event.current.Use();
                        }
                        else if (Event.current.control)
                        {
                            data.SetMap(x, y, brush);
                            Event.current.Use();
                        }
                    }
                }
            }
        }
    }
}