using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using RainScript.VirtualMachine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using RainScript;
using System.Text;
using RainScript.Vector;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

namespace NameSpace
{
    public struct EntitySnapshoot
    {
        public long instance;
        public bool delete;
        public string model;
        public string anim;
        public Vector2 pos;
        public bool face;
        public EntitySnapshoot(long instance, bool delete, string model, string anim, Vector2 pos, bool face)
        {
            this.instance = instance;
            this.delete = delete;
            this.model = model;
            this.anim = anim;
            this.pos = pos;
            this.face = face;
        }
    }
    public class Entity : IEntity
    {
        public bool dirty;
        public bool deleted;
        public long instance;
        public bool reference;
        public string model;
        public string anim;
        public Vector3 position;
        public bool face;
        public EntitySnapshoot GetSnapshoot()
        {
            return new EntitySnapshoot(instance, deleted, model, anim, position, face);
        }
        public void OnReference()
        {
            reference = true;
        }
        public void OnRelease()
        {
            reference = false;
        }
    }
    public class Performer : IPerformer
    {
        private readonly Map map;
        private readonly Action<Vector3> setCamPos;
        private readonly LRPipeline<EntitySnapshoot> pipeline;
        public Performer(LRPipeline<EntitySnapshoot> pipeline, Map map, Action<Vector3> setCamPos)
        {
            this.pipeline = pipeline;
            this.map = map;
            this.setCamPos = setCamPos;
        }

        public long GetMapType(Real2 pos)
        {
            var x = Mathf.RoundToInt((float)pos.x / 2);
            var y = Mathf.RoundToInt((float)pos.y);
            if (x < 0 || y < 0 || x >= map.width || y >= map.height) return 0;
            return map[x, y];
        }
        public void SetCameraPosition(Real2 pos)
        {
            setCamPos(new Vector3((float)pos.x, (float)pos.y, -10));
        }

        public IEntity Item_Create(string model, string anim, bool face, Real2 position)
        {
            var entity = pool.Count > 0 ? pool.Pop() : new Entity();
            entity.dirty = true;
            entity.deleted = false;
            entity.instance = ++index;
            entity.position = new Vector3(position.x, position.y, -1);
            entity.model = model;
            entity.anim = anim;
            entity.face = face;
            entities.Add(entity);
            return entity;
        }
        public void Item_SetAnim(IEntity entity, string anim)
        {
            if (entity is Entity e)
            {
                e.dirty = true;
                e.anim = anim;
            }
        }
        public void Item_SetFace(IEntity entity, bool face)
        {
            if (entity is Entity e)
            {
                e.face = face;
                e.dirty = true;
            }
        }
        public void Item_SetPos(IEntity entity, Real2 pos)
        {
            if (entity is Entity e)
            {
                e.position = new Vector2(pos.x, pos.y);
                e.dirty = true;
            }
        }
        public void Item_Delete(IEntity entity, string anim)
        {
            if (entity is Entity e)
            {
                e.anim = anim;
                e.deleted = true;
                e.dirty = true;
            }
        }

        public void Log(string msg)
        {
            Debug.Log("<color=#00ffcc>" + msg + "</color>");
        }

        public void Flush()
        {
            foreach (var item in entities)
            {
                if (item.dirty)
                {
                    pipeline.En(item.GetSnapshoot());
                    item.dirty = false;
                }
            }
            entities.RemoveAll(v =>
            {
                if (v.deleted) pool.Push(v);
                return v.deleted;
            });
        }
        private long index;
        private readonly Stack<Entity> pool = new Stack<Entity>();
        private readonly List<Entity> entities = new List<Entity>();
    }
    public class GameLogic : IDisposable
    {
        private bool _disposed = false;
        private readonly Operator[] lastOperators;
        private readonly Operator[] operators;
        private readonly IOper oper;
        private readonly FunctionHandle operFH;
        private readonly Kernel kernel;
        private readonly RainScript.DebugAdapter.Debugger debugger;
        private readonly SymbolTable symbolTable;
        private readonly DebugTable debugTable;
        private readonly Performer performer;
        public string msg;
        public GameLogic(Performer performer, int local, int playerCount, int seed, IOper oper)
        {
            this.performer = performer;
            lastOperators = new Operator[playerCount];
            operators = new Operator[playerCount];
            this.oper = oper;
            symbolTable = Load<SymbolTable>("symbol");
            debugTable = Load<DebugTable>("debug");
            //这边回调函数实际上应该通过回调的程序集名加载对应的文件，但这里只有一个程序集，所以简单处理了
            kernel = new Kernel(Load<Library>("library"), null, _ => performer);
            debugger = new RainScript.DebugAdapter.Debugger("RSDemo", kernel, _ => debugTable, _ => symbolTable);
            operFH = kernel.GetFunctionHandle("OnPlayerOperator");
            kernel.OnExit += OnCoroExit;
            using (var initPC = kernel.Invoker(kernel.GetFunctionHandle("InitGame")))
            {
                initPC.SetParameter(0, local);
                initPC.SetParameter(1, playerCount);
                initPC.SetParameter(2, seed);
                initPC.Start(true, false);
            }
            for (int i = 0; i < playerCount; i++)
            {
                SetOper(i, new Operator());
            }

            new Thread(LogicLoop).Start();
        }

        private void OnCoroExit(RainScript.VirtualMachine.StackFrame[] stacks, long code)
        {
            var sb = new StringBuilder();
            sb.AppendLine("携程异常退出，退出码：0x" + code.ToString("X"));
            foreach (var item in stacks)
            {
                symbolTable.GetInfo(item, out var file, out var function, out var line);
                sb.AppendFormat("{0} <color=#ffcc00>{1}</color> line {2}", file, function, line + 1);
                sb.AppendLine();
            }
            Debug.LogError(sb);
        }
        private void SetOper(int pid, Operator oper)
        {
            using (var invoker = kernel.Invoker(operFH))
            {
                invoker.SetParameter(0, pid);
                invoker.SetParameter(1, oper.up);
                invoker.SetParameter(2, oper.down);
                invoker.SetParameter(3, oper.left);
                invoker.SetParameter(4, oper.right);
                invoker.SetParameter(5, oper.fire);
                invoker.SetParameter(6, oper.jump);
                invoker.Start(true, false);
            }
        }
        private void LogicLoop()
        {
            var sw = new Stopwatch();
            while (!_disposed)
            {
                sw.Restart();
                if (oper.TryGetOper(operators))
                {
                    for (int i = 0; i < operators.Length; i++)
                    {
                        if (operators[i] != lastOperators[i])
                        {
                            lastOperators[i] = operators[i];
                            SetOper(i, operators[i]);
                        }
                    }
                    try
                    {
                        kernel.Update();
                        var state = kernel.GetState();
                        msg = string.Format("HTM:{0}\tEC:{1}\tSC:{2}\tHC:{3}\tCC:{4}", state.heapTotalMemory, state.entityCount, state.stringCount, state.handleCount, state.coroutineCount);
                    }
                    catch (Exception e)
                    {
                        var ss = kernel.GetInvokingStackFrames();
                        OnCoroExit(ss, 0x1234);
                        Debug.LogException(e);
                    }
                }
                performer.Flush();
                sw.Stop();
                Thread.Sleep(Mathf.Max(0, (int)(30 - sw.ElapsedMilliseconds - oper.DelayFrame)));
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            kernel?.Dispose();
            debugger?.Dispose();
        }
        private static T Load<T>(string name)
        {
            var ta = Resources.Load<TextAsset>(name);
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream(ta.bytes))
            {
                return (T)bf.Deserialize(ms);
            }
        }
    }
}