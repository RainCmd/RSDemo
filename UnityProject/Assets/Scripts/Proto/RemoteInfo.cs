using System;
using System.Net;


namespace NameSpace
{
    public class RemoteInfo
    {
        public string name;
        public DateTime time;
        public IPEndPoint ip;
        public bool IsActive
        {
            get
            {
                return (DateTime.Now - time).Ticks < 3 * TimeSpan.TicksPerSecond;
            }
        }
        public RemoteInfo(string name, DateTime time, IPEndPoint ip)
        {
            this.name = name;
            this.time = time;
            this.ip = ip;
        }
    }
}