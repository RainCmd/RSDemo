using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;


namespace NameSpace
{
    public class ServerPlayerInfo
    {
        public RemoteInfo remote;
        public Text text;
        public ServerPlayerInfo(RemoteInfo remote, Text text)
        {
            this.remote = remote;
            this.text = text;
        }
    }
    public class HallAgency : IDisposable
    {
        private bool _disposede = false;
        private readonly Socket socket;
        public readonly List<RemoteInfo> remoteInfos = new List<RemoteInfo>();
        public bool roomInfoDirty = false;
        public HallAgency()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, 14567));
            new Thread(Recv).Start();
            new Thread(Heartbeat).Start();
        }
        private void Recv()
        {
            var buf = new byte[2048];
            var ip = new IPEndPoint(IPAddress.Any, 14567);
            while (!_disposede)
            {
                EndPoint remote = ip;
                int size = 0;
                try
                {
                    size = socket.ReceiveFrom(buf, ref remote);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    break;
                }
                var rip = (IPEndPoint)remote;
                var port = BitConverter.ToInt32(buf, 0);
                var name = Encoding.UTF8.GetString(buf, 4, size);
                lock (remoteInfos)
                {
                    var info = remoteInfos.Find(v => v.ip.Address.Equals(rip.Address) && v.ip.Port == rip.Port);
                    if (info == null)
                    {
                        info = new RemoteInfo(name, DateTime.Now, rip);
                        remoteInfos.Add(info);
                        roomInfoDirty = true;
                    }
                    else
                    {
                        if (info.name != name) roomInfoDirty = true;
                        info.name = name;
                        info.time = DateTime.Now;
                    }
                }
            }
        }
        private void Heartbeat()
        {
            while (!_disposede)
            {
                lock (remoteInfos)
                {
                    if (remoteInfos.RemoveAll(v => !v.IsActive) > 0)
                    {
                        roomInfoDirty = true;
                    }
                }
                Thread.Sleep(1000);
            }
        }
        public void Dispose()
        {
            if (_disposede) return;
            _disposede = true;
            socket.Close();
        }
    }
    public class Hall : MonoBehaviour
    {
        private HallAgency hallAgency;
        private void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            hallAgency = new HallAgency();
        }
        private void Update()
        {
            if (hall.activeSelf)
            {
                RefershHallInfo();
            }
            if (room.activeSelf)
            {
                RefershServerRoomInfo();
                RefershClientRoomInfo();
            }
        }
        private void RefershHallInfo()
        {
            if (hallAgency.roomInfoDirty)
            {
                foreach (var item in rooms)
                {
                    pool.Push(item);
                    item.gameObject.SetActive(false);
                }
                rooms.Clear();
                lock (hallAgency.remoteInfos)
                {
                    foreach (var item in hallAgency.remoteInfos)
                    {
                        var room = GetRoom();
                        room.gameObject.SetActive(true);
                        room.SetValue(this, item);
                        rooms.Add(room);
                    }
                    hallAgency.roomInfoDirty = false;
                }
            }
        }
        private void RefershServerRoomInfo()
        {
            if (server != null && server.playerInfoDirty)
            {
                foreach (var item in serverPlayerList)
                {
                    item.text.gameObject.SetActive(false);
                    serverPlayerPool.Push(item);
                }
                serverPlayerList.Clear();
                lock (server.players)
                {
                    foreach (var item in server.players)
                    {
                        var pi = GetServerPlayer();
                        pi.remote = item;
                        pi.text.text = item.name;
                        pi.text.gameObject.SetActive(true);
                        serverPlayerList.Add(pi);
                    }
                }
                server.playerInfoDirty = false;
            }
        }
        private void RefershClientRoomInfo()
        {
            if (client != null)
            {
                if (client.playerDirty)
                {
                    foreach (var item in clientPlayerList)
                    {
                        item.gameObject.SetActive(false);
                        clientPlayerPool.Push(item);
                    }
                    clientPlayerList.Clear();
                    lock (client.players)
                    {
                        foreach (var item in client.players)
                        {
                            var p = GetClientPlayer();
                            p.gameObject.SetActive(true);
                            p.text = item;
                        }
                    }
                    client.playerDirty = false;
                }
                else if (client.playing)
                {
                    game.StartGame(client);
                    client = null;
                    Destroy(gameObject);
                }
            }
        }
        private void OnDestroy()
        {
            hallAgency.Dispose();
            server?.Dispose();
            client?.Dispose();
        }

        private Stack<Room> pool = new Stack<Room>();
        private List<Room> rooms = new List<Room>();
        private Room GetRoom()
        {
            if (pool.Count > 0) return pool.Pop();
            else
            {
                var go = Instantiate(prefab.gameObject, content);
                return go.GetComponent<Room>();
            }
        }
        private Stack<ServerPlayerInfo> serverPlayerPool = new Stack<ServerPlayerInfo>();
        private List<ServerPlayerInfo> serverPlayerList = new List<ServerPlayerInfo>();
        private ServerPlayerInfo GetServerPlayer()
        {
            if (serverPlayerPool.Count > 0) return serverPlayerPool.Pop();
            var go = Instantiate(playerPrefab, playerContent);
            return new ServerPlayerInfo(null, go.GetComponent<Text>());
        }
        private Stack<Text> clientPlayerPool = new Stack<Text>();
        private List<Text> clientPlayerList = new List<Text>();
        private Text GetClientPlayer()
        {
            if (clientPlayerPool.Count > 0) return clientPlayerPool.Pop();
            var go = Instantiate(playerPrefab, playerContent);
            return go.GetComponent<Text>();
        }
        #region Hall
        public Game game;
        public GameObject hall, room;
        public InputField roomName;
        public Room prefab;
        public RectTransform content;
        private RoomServer server;
        private RoomClient client;
        public RectTransform playerContent;
        public Text playerPrefab;
        public GameObject startGameBtn;
        public void CreateRoom()
        {
            if (!string.IsNullOrEmpty(roomName.text))
            {
                ShowMbx("输入游戏名", "", name => CreateRoom(roomName.text, name));
            }
        }
        private void CreateRoom(string roomName, string playerName)
        {
            server = new RoomServer(roomName, playerName);
            hall.SetActive(false);
            room.SetActive(true);
            startGameBtn.SetActive(true);
        }
        public void OnStartGameClick()
        {
            server.StartGame(new System.Random().Next());
            game.StartGame(server);
            server = null;
            Destroy(gameObject);
        }
        public void OnClickRoom(RemoteInfo info)
        {
            ShowMbx("加入房间 " + info.name, "", name => JoinRoom(name, info));
        }
        private void JoinRoom(string name, RemoteInfo info)
        {
            client = new RoomClient(info, name);
            hall.SetActive(false);
            room.SetActive(true);
        }
        #endregion
        #region MBX
        private Action<string> mbx_ok;
        public GameObject mbx;
        public Text mbxTitle;
        public InputField mbxName;
        private void ShowMbx(string msg, string name, Action<string> ok)
        {
            mbx.SetActive(true);
            mbxTitle.text = msg;
            mbxName.text = name;
            mbx_ok = ok;
        }
        public void OnMBXOKClick()
        {
            if (!string.IsNullOrEmpty(mbxName.text))
            {
                mbx_ok?.Invoke(mbxName.text);
                mbx.SetActive(false);
            }
        }
        #endregion
    }
}