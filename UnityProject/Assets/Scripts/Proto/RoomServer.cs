using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace NameSpace
{
    public struct FrameOper
    {
        public DateTime time;
        public int flag;
        public int frame;
        public readonly Operator[] operators;
        public bool IsActive
        {
            get
            {
                return (DateTime.Now - time).Ticks < 3 * TimeSpan.TicksPerSecond;
            }
        }
        public FrameOper(Operator[] operators)
        {
            time = default;
            flag = 0;
            frame = 0;
            this.operators = operators;
        }
    }
    public class RoomServer : IDisposable, IOper
    {
        private bool _disposed = false;
        private readonly Socket socket;
        public readonly string roomName;
        public readonly string playerName;
        public readonly int port = 38465;
        public readonly List<RemoteInfo> players = new List<RemoteInfo>();
        public int seed;
        public int selfPlayerId;
        private int playerStates;
        private Operator[] operators;
        private List<FrameOper> frameOpers = new List<FrameOper>();
        public bool playerInfoDirty = true;
        private int frame = 0;
        private readonly RemoteInfo self;
        private readonly byte[] buffer = new byte[4096];
        private bool playing = false;
        public RoomServer(string roomName, string playerName)
        {
            this.roomName = roomName;
            this.playerName = playerName;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        rebind:
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch (Exception e)
            {
                port++;
                Debug.LogException(e);
                goto rebind;
            }
            players.Add(self = new RemoteInfo(playerName, DateTime.Now, null));
            new Thread(Accept).Start();
            new Thread(BR).Start();
            new Thread(Heartbeat).Start();
        }
        private void Accept()
        {
            var ip = new IPEndPoint(IPAddress.Any, port);
            var buf = new byte[2048];
            while (!_disposed)
            {
                EndPoint remote = ip;
                var size = socket.ReceiveFrom(buf, ref remote);
                var rip = (IPEndPoint)remote;
                var reader = new PReader(buf);
                switch (reader.ReadProto())
                {
                    case Proto.JoinRoom:
                        {
                            if (playing) break;
                            var name = reader.ReadStr();
                            lock (players)
                            {
                                var info = players.Find(v => v.ip != null && v.ip.Address.Equals(rip.Address) && v.ip.Port == rip.Port);
                                if (info == null)
                                {
                                    info = new RemoteInfo(name, DateTime.Now, rip);
                                    players.Add(info);
                                    playerInfoDirty = true;
                                }
                                else
                                {
                                    if (name != info.name)
                                    {
                                        playerInfoDirty = true;
                                        info.name = name;
                                    }
                                    info.time = DateTime.Now;
                                }
                                lock (this.buffer)
                                {
                                    var writer = new PWriter(this.buffer);
                                    writer.Write(Proto.UpdateRoom);
                                    writer.Write(players.Count);
                                    foreach (var item in players)
                                    {
                                        writer.Write(item.name);
                                    }
                                    foreach (var item in players)
                                    {
                                        Send(writer, item);
                                    }
                                }
                            }
                        }
                        break;
                    case Proto.Heartbeat:
                        lock (players)
                        {
                            var info = players.Find(v => v.ip != null && v.ip.Address.Equals(rip.Address) && v.ip.Port == rip.Port);
                            if (info != null)
                            {
                                info.time = DateTime.Now;
                                lock (this.buffer)
                                {
                                    var writer = new PWriter(this.buffer);
                                    writer.Write(Proto.Heartbeat);
                                    Send(writer, info);
                                }
                            }
                        }
                        break;
                    case Proto.UpdateRoom:
                        break;
                    case Proto.StartGame:
                        break;
                    case Proto.UpdateOper:
                        lock (players)
                        {
                            var index = players.FindIndex(v => v.ip != null && v.ip.Address.Equals(rip.Address) && v.ip.Port == rip.Port);
                            if (index >= 0)
                            {
                                SetOperator(index, reader.ReadOper());
                            }
                        }
                        break;
                    case Proto.FrameRecv:
                        {
                            int index;
                            lock (players)
                            {
                                index = players.FindIndex(v => v.ip != null && v.ip.Address.Equals(rip.Address) && v.ip.Port == rip.Port);
                            }
                            if (index < 0) break;
                            var frame = reader.ReadInt();
                            lock (frameOpers)
                            {
                                var fidx = frameOpers.FindIndex(v => v.frame == frame);
                                var fo = frameOpers[fidx];
                                fo.flag &= ~(1 << index);
                                if (fo.flag > 0)
                                {
                                    frameOpers[fidx] = fo;
                                }
                                else
                                {
                                    lock (foPool)
                                    {
                                        foPool.Push(fo);
                                    }
                                    frameOpers.RemoveAt(fidx);
                                }
                            }
                        }
                        break;
                    case Proto.FrameReSend:
                        {
                            var frame = reader.ReadInt();
                            lock (frameOpers)
                            {
                                var fidx = frameOpers.FindIndex(v => v.frame == frame);
                                if (fidx >= 0)
                                {
                                    var fo = frameOpers[fidx];
                                    lock (this.buffer)
                                    {
                                        var writer = new PWriter(this.buffer);
                                        writer.Write(Proto.UpdateOper);
                                        writer.Write(frame);
                                        foreach (var item in fo.operators)
                                        {
                                            writer.Write(item);
                                        }
                                        Send(writer, remote);
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        private void BR()
        {
            var ib = BitConverter.GetBytes(port);
            var nb = Encoding.UTF8.GetBytes(roomName);
            var buf = new byte[ib.Length + nb.Length];
            Array.Copy(ib, buf, ib.Length);
            Array.Copy(nb, 0, buf, ib.Length, nb.Length);
            var ip = new IPEndPoint(IPAddress.Broadcast, 14567);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            while (!_disposed && !playing)
            {
                socket.SendTo(buf, buf.Length, SocketFlags.None, ip);
                Thread.Sleep(1000);
            }
        }
        private void Heartbeat()
        {
            while (!_disposed)
            {
                lock (players)
                {
                    if (playing)
                    {
                        for (int i = 0; i < players.Count; i++)
                        {
                            var player = players[i];
                            if (player.ip != null && !player.IsActive)
                            {
                                player.ip = null;
                            }
                        }
                    }
                    else if (players.RemoveAll(v => v != self && !v.IsActive) > 0)
                    {
                        playerInfoDirty = true;
                    }
                }
                Thread.Sleep(1000);
            }
        }
        public void StartGame(int seed)
        {
            lock (buffer)
            {
                lock (players)
                {
                    this.seed = seed;
                    playing = true;
                    operators = new Operator[players.Count];
                    selfPlayerId = players.FindIndex(v => v == self);
                    playerStates = (1 << (operators.Length + 1)) - 1;
                    playerStates &= ~(1 << selfPlayerId);

                    for (int i = 0; i < players.Count; i++)
                    {
                        if (i != selfPlayerId)
                        {
                            var writer = new PWriter(buffer);
                            writer.Write(Proto.StartGame);
                            writer.Write(i);
                            writer.Write(seed);
                            writer.Write(players.Count);
                            foreach (var item in players)
                            {
                                writer.Write(item.name);
                            }
                            Send(writer, players[i]);
                        }
                    }
                }
            }
        }
        public void Send(PWriter writer, RemoteInfo info)
        {
            if (info.ip != null)
                Send(writer, info.ip);
        }
        public void Send(PWriter writer, EndPoint ip)
        {
            socket.SendTo(buffer, writer.position, SocketFlags.None, ip);
        }
        public void SetOperator(int pid, Operator oper)
        {
            lock (operators)
            {
                operators[pid] = oper;
            }
        }
        public FrameOper GetFrameOper()
        {
            var result = foPool.Count > 0 ? foPool.Pop() : new FrameOper(new Operator[operators.Length]);
            result.frame = frame++;
            result.time = DateTime.Now;
            result.flag = playerStates;
            lock (operators)
            {
                Array.Copy(operators, result.operators, operators.Length);
            }
            if (result.flag != 0)
            {
                frameOpers.Add(result);
                lock (buffer)
                {
                    var writer = new PWriter(buffer);
                    writer.Write(Proto.UpdateOper);
                    writer.Write(result.frame);
                    foreach (var item in result.operators)
                    {
                        writer.Write(item);
                    }
                    lock (players)
                    {
                        foreach (var item in players)
                        {
                            if (item.ip != null)
                                Send(writer, item);
                        }
                    }
                }
                lock (frameOpers)
                {
                    lock (foPool)
                    {
                        frameOpers.RemoveAll(v =>
                        {
                            if (v.IsActive) return false;
                            foPool.Push(v);
                            return true;
                        });
                    }
                    frameOpers.Add(result);
                }
            }
            return result;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            socket.Close();
        }

        public int DelayFrame { get { return 0; } }
        public bool TryGetOper(Operator[] operators)
        {
            var oper = GetFrameOper();
            Array.Copy(oper.operators, operators, operators.Length);
            lock (foPool)
            {
                foPool.Push(oper);
            }
            return true;
        }

        private Stack<FrameOper> foPool = new Stack<FrameOper>();
    }
}