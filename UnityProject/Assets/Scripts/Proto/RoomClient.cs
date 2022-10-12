using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace NameSpace
{
    public class RoomClient : IDisposable, IOper
    {
        private bool _disposed = false;
        public readonly string name;
        public readonly List<string> players = new List<string>();
        public int selfPID;
        public int seed;
        public bool playerDirty = false;
        public readonly RemoteInfo server;
        public readonly Socket socket;
        private readonly byte[] buffer = new byte[2048];
        public bool playing = false;
        private int frame = 0;
        private int maxFrame = 0;
        private List<FrameOper> frameOpers = new List<FrameOper>();
        public readonly LRPipeline<FrameOper> foPool = new LRPipeline<FrameOper>();
        public RoomClient(RemoteInfo info, string name)
        {
            server = info;
            this.name = name;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var port = 14567;
        rebind:
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch (Exception)
            {
                port++;
                goto rebind;
            }
            new Thread(Recv).Start();
            new Thread(Heartbeat).Start();
            Thread.Sleep(10);
            lock (buffer)
            {
                var writer = new PWriter(buffer);
                writer.Write(Proto.JoinRoom);
                writer.Write(name);
                Send(writer);
            }
        }
        private void Recv()
        {
            var buffer = new byte[4096];
            while (!_disposed)
            {
                EndPoint remote = server.ip;
                socket.ReceiveFrom(buffer, ref remote);
                var reader = new PReader(buffer);
                switch (reader.ReadProto())
                {
                    case Proto.JoinRoom:
                        break;
                    case Proto.Heartbeat:
                        server.time = DateTime.Now;
                        break;
                    case Proto.UpdateRoom:
                        lock (players)
                        {
                            players.Clear();
                            var cnt = reader.ReadInt();
                            while (cnt-- > 0)
                            {
                                players.Add(reader.ReadStr());
                            }
                        }
                        playerDirty = true;
                        break;
                    case Proto.StartGame:
                        lock (players)
                        {
                            players.Clear();
                            selfPID = reader.ReadInt();
                            seed = reader.ReadInt();
                            var cnt = reader.ReadInt();
                            while (cnt-- > 0)
                            {
                                players.Add(reader.ReadStr());
                            }
                        }
                        playing = true;
                        break;
                    case Proto.UpdateOper:
                        {
                            if (!foPool.TryDe(out var result))
                            {
                                result = new FrameOper(new Operator[players.Count]);
                            }
                            result.time = DateTime.Now;
                            result.frame = reader.ReadInt();
                            maxFrame = Mathf.Max(result.frame, maxFrame);
                            for (int i = 0; i < players.Count; i++)
                                result.operators[i] = reader.ReadOper();
                            lock (frameOpers)
                            {
                                frameOpers.Add(result);
                            }
                            lock (this.buffer)
                            {
                                var writer = new PWriter(this.buffer);
                                writer.Write(Proto.FrameRecv);
                                writer.Write(result.frame);
                                Send(writer);
                            }
                        }
                        break;
                    case Proto.FrameRecv:
                        break;
                    case Proto.FrameReSend:
                        break;
                    default:
                        break;
                }
            }
        }
        private void Heartbeat()
        {
            while (!_disposed)
            {
                if (server.IsActive)
                {
                    lock (buffer)
                    {
                        var writer = new PWriter(buffer);
                        writer.Write(Proto.Heartbeat);
                        Send(writer);
                    }
                }
                Thread.Sleep(1000);
            }
        }
        public void Send(PWriter writer)
        {
            socket.SendTo(buffer, writer.position, SocketFlags.None, server.ip);
        }
        public void SendOperator(Operator oper)
        {
            lock (buffer)
            {
                var writer = new PWriter(buffer);
                writer.Write(Proto.UpdateOper);
                writer.Write(oper);
                Send(writer);
            }
        }
        public int DelayFrame { get { return maxFrame - frame; } }
        public bool TryGetOper(Operator[] operators)
        {
            lock (frameOpers)
            {
                frameOpers.RemoveAll(v =>
                {
                    if (v.IsActive) return false;
                    foPool.En(v);
                    return true;
                });
                var index = frameOpers.FindIndex(v => v.frame == frame);
                if (index < 0)
                {
                    lock (buffer)
                    {
                        var writer = new PWriter(this.buffer);
                        writer.Write(Proto.FrameReSend);
                        writer.Write(frame);
                        Send(writer);
                    }
                    return false;
                }
                var oper = frameOpers[index];
                Array.Copy(oper.operators, operators, operators.Length);
                frameOpers.RemoveAt(index);
                foPool.En(oper);
                frame++;
                return true;
            }
        }
        public void Dispose()
        {
            if (!_disposed) return;
            _disposed = true;
            socket.Close();
        }
    }
}