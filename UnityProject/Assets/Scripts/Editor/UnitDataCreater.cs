using UnityEditor.ProjectWindowCallback;
using UnityEditor;
using System.IO;

namespace NameSpace
{
    public class UnitDataCreater : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var data = CreateInstance<UnitData>();
            data.name = Path.GetFileName(pathName);
            AssetDatabase.CreateAsset(data, pathName + ".asset");
            AssetDatabase.Refresh();
        }
        [MenuItem("Assets/Create/单位数据")]
        private static void CreateMenu()
        {
            var action = CreateInstance<UnitDataCreater>();
            var defName = "UnitData";
            if (Selection.activeObject && AssetDatabase.Contains(Selection.activeObject))
            {
                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path))
                {
                    if (Selection.activeObject.GetType() == typeof(DefaultAsset)) defName = path + "/" + defName;
                    else defName = path.Substring(0, path.LastIndexOf('/') + 1) + defName;
                }
                else defName = "Assets/" + defName;
            }
            else defName = "Assets/" + defName;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, defName, null, "");
        }
    }
}