namespace NameSpace
{
    public interface IOper
    {
        int DelayFrame { get; }
        bool TryGetOper(Operator[] operators);
    }
    public struct Operator
    {
        public bool up, down, left, right;
        public bool fire, jump;
        public int ToInt()
        {
            var result = 0;
            if (up) result |= 1;
            if (down) result |= 2;
            if (left) result |= 4;
            if (right) result |= 8;
            if (fire) result |= 16;
            if (jump) result |= 32;
            return result;
        }
        public Operator(int value)
        {
            up = (value & 1) > 0;
            down = (value & 2) > 0;
            left = (value & 4) > 0;
            right = (value & 8) > 0;
            fire = (value & 16) > 0;
            jump = (value & 32) > 0;
        }
        public static bool operator ==(Operator left, Operator right)
        {
            return left.up == right.up && left.down == right.down && left.left == right.left && left.right == right.right &&
                left.fire == right.fire && left.jump == right.jump;
        }
        public static bool operator !=(Operator left, Operator right)
        {
            return !(left == right);
        }
        public override bool Equals(object obj)
        {
            return obj is Operator @operator &&
                   up == @operator.up &&
                   down == @operator.down &&
                   left == @operator.left &&
                   right == @operator.right &&
                   fire == @operator.fire &&
                   jump == @operator.jump;
        }

        public override int GetHashCode()
        {
            int hashCode = -153618616;
            hashCode = hashCode * -1521134295 + up.GetHashCode();
            hashCode = hashCode * -1521134295 + down.GetHashCode();
            hashCode = hashCode * -1521134295 + left.GetHashCode();
            hashCode = hashCode * -1521134295 + right.GetHashCode();
            hashCode = hashCode * -1521134295 + fire.GetHashCode();
            hashCode = hashCode * -1521134295 + jump.GetHashCode();
            return hashCode;
        }
    }
}