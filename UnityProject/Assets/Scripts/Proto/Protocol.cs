using System;
using System.Text;


namespace NameSpace
{
    public enum Proto
    {
        JoinRoom,
        Heartbeat,
        UpdateRoom,
        StartGame,
        UpdateOper,
        FrameRecv,
        FrameReSend,
    }
    public struct PWriter
    {
        public int position;
        private readonly byte[] buffer;
        public PWriter(byte[] buffer)
        {
            position = 0;
            this.buffer = buffer;
        }
        public void Write(Proto proto)
        {
            buffer[position++] = (byte)proto;
        }
        public void Write(bool value)
        {
            buffer[position++] = (byte)(value ? 1 : 0);
        }
        public void Write(int value)
        {
            var buf = BitConverter.GetBytes(value);
            Array.Copy(buf, 0, buffer, position, buf.Length);
            position += buf.Length;
        }
        public void Write(string value)
        {
            var buf = Encoding.UTF8.GetBytes(value);
            Write(buf.Length);
            Array.Copy(buf, 0, buffer, position, buf.Length);
            position += buf.Length;
        }
        public void Write(Operator oper)
        {
            Write(oper.ToInt());
        }
    }
    public struct PReader
    {
        private int position;
        private readonly byte[] buffer;
        public PReader(byte[] buffer)
        {
            position = 0;
            this.buffer = buffer;
        }
        public Proto ReadProto()
        {
            return (Proto)buffer[position++];
        }
        public bool ReadBool()
        {
            return buffer[position++] > 0;
        }
        public int ReadInt()
        {
            var result = BitConverter.ToInt32(buffer, position);
            position += 4;
            return result;
        }
        public string ReadStr()
        {
            var length = ReadInt();
            var result = Encoding.UTF8.GetString(buffer, position, length);
            position += length;
            return result;
        }
        public Operator ReadOper()
        {
            return new Operator(ReadInt());
        }
    }
}