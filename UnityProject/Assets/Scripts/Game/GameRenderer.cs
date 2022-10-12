using System;
using System.Collections.Generic;
using UnityEngine;


namespace NameSpace
{
    public class EntityRenderer
    {
        public UnitData data;
        public readonly SpriteRenderer renderer;
        private string model;
        private string anim;
        private int animIdx;
        private int spriteIdx;
        private float inv;
        private bool death;
        public EntityRenderer()
        {
            var go = new GameObject("", new System.Type[] { typeof(SpriteRenderer) });
            renderer = go.GetComponent<SpriteRenderer>();
        }
        public void Set(EntitySnapshoot snapshoot)
        {
            death = snapshoot.delete;
            if (model != snapshoot.model)
            {
                model = snapshoot.model;
                data = Resources.Load<UnitData>(snapshoot.model);
            }
            if (anim != snapshoot.anim)
            {
                anim = snapshoot.anim;
                var idx = data.anims.FindIndex(v => v.name == anim);
                if (idx >= 0)
                {
                    animIdx = idx;
                    inv = data.anims[idx].inv;
                    spriteIdx = 0;
                    renderer.sprite = data.anims[idx].sprites[0];
                }
            }
            renderer.transform.position = snapshoot.pos;
            renderer.transform.localScale = new Vector3(snapshoot.face ? 1 : -1, 1, 1);
        }
        public bool Update()
        {
            inv -= Time.deltaTime;
            if (inv <= 0)
            {
                inv += data.anims[animIdx].inv;
                spriteIdx++;
                spriteIdx %= data.anims[animIdx].sprites.Length;
                renderer.sprite = data.anims[animIdx].sprites[spriteIdx];
                if (spriteIdx == 0 && death)
                {
                    renderer.gameObject.SetActive(false);
                    model = "";
                    anim = "";
                    pool.Push(this);
                    return true;
                }
            }
            return false;
        }
        public static EntityRenderer GetRenderer()
        {
            return pool.Count > 0 ? pool.Pop() : new EntityRenderer();
        }
        private static Stack<EntityRenderer> pool = new Stack<EntityRenderer>();
    }
    public class GameRenderer
    {
        private LRPipeline<EntitySnapshoot> pipeline;
        private readonly Dictionary<long, EntityRenderer> renderers = new Dictionary<long, EntityRenderer>();
        private readonly List<long> delList = new List<long>();
        public GameRenderer(LRPipeline<EntitySnapshoot> pipeline)
        {
            this.pipeline = pipeline;
        }
        public void Update()
        {
            while (pipeline.TryDe(out var snapshoot))
            {
                if (!renderers.TryGetValue(snapshoot.instance, out var renderer))
                {
                    renderer = EntityRenderer.GetRenderer();
                    renderer.renderer.gameObject.SetActive(true);
                    renderer.renderer.gameObject.name = snapshoot.model;
                    renderers[snapshoot.instance] = renderer;
                }
                renderer.Set(snapshoot);
            }
            foreach (var item in renderers)
            {
                if (item.Value.Update())
                {
                    delList.Add(item.Key);
                }
            }
            if (delList.Count > 0)
            {
                foreach (var item in delList)
                {
                    renderers.Remove(item);
                }
                delList.Clear();
            }
        }
    }
}