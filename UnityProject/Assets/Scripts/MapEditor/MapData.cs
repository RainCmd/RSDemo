using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace NameSpace
{
    [Serializable]
    public unsafe struct Map
    {
        public long width, height;
        public long[] map;
        public long this[int x, int y]
        {
            get
            {
                return map[x * height + y];
            }
            set
            {
                map[x * height + y] = value;
            }
        }
        public Map(long width, long height)
        {
            this.width = width;
            this.height = height;
            map = new long[width * height];
        }
        public byte[] Ser()
        {
            var bytes = new byte[width * height * 8 + 16];
            fixed (byte* ptr = bytes)
            {
                var p = (long*)ptr;
                *p++ = width;
                *p++ = height;
                foreach (var item in map) *p++ = item;
            }
            return bytes;
        }
        public static Map Des(byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                var p = (long*)ptr;
                var result = new Map(p[0], p[1]);
                p += 2;
                for (int i = 0; i < result.map.Length; i++) result.map[i] = *p++;
                return result;
            }
        }
    }
    public class MapData : MonoBehaviour
    {
        public Map map;
        public Sprite[] sprites;
        private SpriteRenderer[] renderers = new SpriteRenderer[0];
        private Stack<SpriteRenderer> rendererPool = new Stack<SpriteRenderer>();
        public SpriteRenderer this[int x, int y]
        {
            get
            {
                if (x >= 0 && x < map.width && y >= 0 && y < map.height) return renderers[x * map.height + y];
                else return null;
            }
        }
        public void Show(bool show)
        {
            foreach (var item in renderers)
                item.gameObject.SetActive(show);
        }
        public void SetMap(int x, int y, long map)
        {
            if (x >= 0 && x < this.map.width && y >= 0 && y < this.map.height)
            {
                var index = x * this.map.height + y;
                this.map[x, y] = map;
                renderers[index].sprite = sprites[map];
            }
        }
        public void Init(long width, long height)
        {
            LoadMap(new Map(width, height));
        }
        public void LoadMap(Map map)
        {
            this.map = map;
            if (renderers != null)
                foreach (var item in renderers)
                {
                    if (item)
                    {
                        item.gameObject.SetActive(false);
                        rendererPool.Push(item);
                    }
                }
            renderers = new SpriteRenderer[map.width * map.height];
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    var index = x * map.height + y;
                    renderers[index] = GetRenderer();
                    renderers[index].transform.position = new Vector3(32 * x, 16 * y) / 16f;
                    renderers[index].name = string.Format("{0},{1}", x, y);
                }
            }
            RefreshMap();
        }
        public void RefreshMap()
        {
            for (int i = 0; i < map.map.Length; i++)
            {
                renderers[i].sprite = sprites[map.map[i]];
            }
        }
        private SpriteRenderer GetRenderer()
        {
            if (rendererPool.Count > 0)
            {
                var result = rendererPool.Pop();
                result.gameObject.SetActive(true);
                return result;
            }
            else
            {
                var result = new GameObject("SR").AddComponent<SpriteRenderer>();
                result.transform.SetParent(transform);
                return result;
            }
        }
        public void Clear()
        {
            foreach (var item in renderers)
            {
                DestroyImmediate(item.gameObject);
            }
            renderers = null;
            while (rendererPool.Count > 0)
            {
                DestroyImmediate(rendererPool.Pop());
            }
        }
        private void OnDestroy()
        {
            Clear();
        }
    }
}