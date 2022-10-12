using UnityEngine;
using UnityEngine.EventSystems;

namespace NameSpace
{
    public class Game : MonoBehaviour
    {
        public Camera gameCamera;
        private Map map;
        public EventTrigger dir, fire, jump;
        private RoomClient client;
        private RoomServer server;
        private Operator last, current;
        private GameLogic gameLogic;
        private GameRenderer gameRenderer;
        private Vector3 cameraPos;
        public void StartGame(RoomClient client)
        {
            this.client = client;
            StartGame(client.selfPID, client.players.Count, client.seed, client);
        }
        public void StartGame(RoomServer server)
        {
            this.server = server;
            StartGame(server.selfPlayerId, server.players.Count, server.seed, server);
        }
        private void StartGame(int local, int playerCount, int seed, IOper oper)
        {
            LoadMap();
            var pipeline = new LRPipeline<EntitySnapshoot>();
            gameLogic = new GameLogic(new Performer(pipeline, map, v => cameraPos = v), local, playerCount, seed, oper);
            gameObject.SetActive(true);
            gameRenderer = new GameRenderer(pipeline);
        }
        private void LoadMap()
        {
            var md = Resources.Load<TextAsset>("mapData");
            map = Map.Des(md.bytes);
            var ms = Resources.LoadAll<Sprite>("map");
            var root = new GameObject("map").transform;
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    var go = new GameObject(string.Format("{0},{1}", x, y));
                    var s = go.AddComponent<SpriteRenderer>();
                    s.sprite = ms[map[x, y]];
                    go.transform.SetParent(root);
                    go.transform.position = new Vector3(x * 2, y, 1);
                }
            }
        }
        private void Start()
        {
            var te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.PointerDown;
            te.callback.AddListener(e => OnFire(true));
            fire.triggers.Add(te);

            te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.PointerUp;
            te.callback.AddListener(e => OnFire(false));
            fire.triggers.Add(te);

            te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.PointerDown;
            te.callback.AddListener(e => OnJump(true));
            jump.triggers.Add(te);

            te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.PointerUp;
            te.callback.AddListener(e => OnJump(false));
            jump.triggers.Add(te);

            te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.BeginDrag;
            te.callback.AddListener(e => OnDir(e as PointerEventData));
            dir.triggers.Add(te);

            te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.Drag;
            te.callback.AddListener(e => OnDir(e as PointerEventData));
            dir.triggers.Add(te);

            te = new EventTrigger.Entry();
            te.eventID = EventTriggerType.EndDrag;
            te.callback.AddListener(e => ClearDir());
            dir.triggers.Add(te);
        }
        private void OnGUI()
        {
            GUI.skin.label.fontSize = 32;
            GUILayout.Label(gameLogic.msg);
        }
        private void Update()
        {
            if (last != current)
            {
                last = current;
                if (server != null)
                {
                    server.SetOperator(server.selfPlayerId, current);
                }
                if (client != null)
                {
                    client.SendOperator(current);
                }
            }
            gameRenderer?.Update();
        }
        private void LateUpdate()
        {
            gameCamera.transform.position = cameraPos;
        }
        private void OnDestroy()
        {
            server?.Dispose();
            client?.Dispose();
            gameLogic?.Dispose();
        }
        private void OnFire(bool fire)
        {
            current.fire = fire;
        }
        private void OnJump(bool jump)
        {
            current.jump = jump;
        }
        private void OnDir(PointerEventData pointer)
        {
            var dir = pointer.position - (Vector2)this.dir.transform.position;
            if (dir.sqrMagnitude > 1024)
            {
                current.up = dir.y / Mathf.Abs(dir.x) > .5f;
                current.down = dir.y / Mathf.Abs(dir.x) < -.5f;
                current.left = dir.x / Mathf.Abs(dir.y) < -.5f;
                current.right = dir.x / Mathf.Abs(dir.y) > .5f;
            }
            else ClearDir();
        }
        private void ClearDir()
        {
            current.up = current.down = current.left = current.right = false;
        }
    }
}