using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using RainScript.Compiler;
using System.Runtime.Serialization.Formatters.Binary;
using System;

namespace NameSpace
{
    public class ScriptGenerator
    {
        private static string root = Application.dataPath + "/RS/";
        private class FInfo : IFileInfo
        {
            public string Path { get; private set; }
            public string Context { get; private set; }
            public FInfo(string path)
            {
                Path = path.Substring(root.Length).Replace('\\', '/');
                using (var fs = File.OpenText(path))
                    Context = fs.ReadToEnd();
            }
        }
        private static IEnumerable<IFileInfo> GetFiles()
        {
            foreach (var item in Directory.GetFiles(root, "*.rain", SearchOption.AllDirectories))
            {
                yield return new FInfo(item);
            }
        }
        private static IEnumerable<ReferenceLibrary> GetRefs()
        {
            yield break;
        }
        [RuntimeInitializeOnLoadMethod]
        [MenuItem("编译/编译RS")]
        private static void RSSrc2Res()
        {
            var builder = new Builder("RSDemo", GetFiles(), GetRefs());
            try
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                builder.Compile(new CompilerCommand(true, true, false));
                sw.Stop();
                Debug.Log("编译耗时：" + sw.Elapsed);
            }
            catch (Exception)
            {
                foreach (var item in builder.exceptions)
                {
                    Debug.LogErrorFormat("编译错误：{0}\t{1}\r\n{2} <color=#ff0000>{3}</color>", item.code, item.message, item.path, item.line);
                }
                throw;
            }
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, builder.Library);
                File.WriteAllBytes(Application.dataPath + "/Resources/library.bytes", ms.ToArray());
            }
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, builder.SymbolTable);
                File.WriteAllBytes(Application.dataPath + "/Resources/symbol.bytes", ms.ToArray());
            }
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, builder.DebugTable);
                File.WriteAllBytes(Application.dataPath + "/Resources/debug.bytes", ms.ToArray());
            }

            AssetDatabase.Refresh();
        }
    }
}