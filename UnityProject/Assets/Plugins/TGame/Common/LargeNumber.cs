using System;
using System.Text;

namespace TGame.Common
{
    [Serializable]
    public class LargeInt : IComparable<LargeInt>, IEquatable<LargeInt>
    {
        public const int Mul = 100000;
        public int[] IntValues;

        public LargeInt() : this(0) { }

        public LargeInt(long value)
        {
            if (value == 0)
            {
                IntValues = Array.Empty<int>();
                return;
            }
            var digits = new System.Collections.Generic.List<int>();
            long v = value;
            while (v > 0)
            {
                digits.Add((int)(v % Mul));
                v /= Mul;
            }
            IntValues = digits.ToArray();
        }

        public LargeInt(int[] values)
        {
            if (values == null || values.Length == 0)
            {
                IntValues = Array.Empty<int>();
                return;
            }
            IntValues = (int[])values.Clone();
            TrimEnd();
        }

        private void TrimEnd()
        {
            if (IntValues == null || IntValues.Length == 0)
            {
                IntValues = Array.Empty<int>();
                return;
            }
            int last = IntValues.Length - 1;
            while (last >= 0 && IntValues[last] == 0)
                last--;
            if (last != IntValues.Length - 1)
                Array.Resize(ref IntValues, last + 1);
            if (IntValues.Length == 0)
                IntValues = Array.Empty<int>();
        }

        public override string ToString()
        {
            if (IntValues == null || IntValues.Length == 0)
                return "0";
            var sb = new StringBuilder();
            sb.Append(IntValues[IntValues.Length - 1]);
            for (int i = IntValues.Length - 2; i >= 0; i--)
                sb.Append(IntValues[i].ToString("D5"));
            return sb.ToString();
        }

        public int CompareTo(LargeInt other)
        {
            if (other == null) return 1;
            int len1 = IntValues?.Length ?? 0;
            int len2 = other.IntValues?.Length ?? 0;
            if (len1 != len2) return len1 > len2 ? 1 : -1;
            for (int i = len1 - 1; i >= 0; i--)
            {
                if (IntValues[i] != other.IntValues[i])
                    return IntValues[i] > other.IntValues[i] ? 1 : -1;
            }
            return 0;
        }

        public bool Equals(LargeInt other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return obj is LargeInt other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (IntValues == null || IntValues.Length == 0) return 0;
            int h = 17;
            for (int i = IntValues.Length - 1; i >= 0; i--)
                h = h * 31 + IntValues[i];
            return h;
        }

        public static LargeInt operator +(LargeInt a, LargeInt b)
        {
            if (a == null || a.IntValues == null || a.IntValues.Length == 0) return b ?? new LargeInt();
            if (b == null || b.IntValues == null || b.IntValues.Length == 0) return a;
            int maxLen = Math.Max(a.IntValues.Length, b.IntValues.Length);
            var result = new int[maxLen + 1];
            int carry = 0;
            for (int i = 0; i < maxLen; i++)
            {
                int av = i < a.IntValues.Length ? a.IntValues[i] : 0;
                int bv = i < b.IntValues.Length ? b.IntValues[i] : 0;
                int sum = av + bv + carry;
                result[i] = sum % Mul;
                carry = sum / Mul;
            }
            if (carry > 0)
                result[maxLen] = carry;
            var lr = new LargeInt();
            lr.IntValues = result;
            lr.TrimEnd();
            return lr;
        }

        public static LargeInt operator -(LargeInt a, LargeInt b)
        {
            if (a == null || a.IntValues == null || a.IntValues.Length == 0)
                return new LargeInt();
            if (b == null || b.IntValues == null || b.IntValues.Length == 0)
                return a;
            if (a.CompareTo(b) < 0) return new LargeInt();
            int maxLen = Math.Max(a.IntValues.Length, b.IntValues.Length);
            var result = new int[maxLen];
            int borrow = 0;
            for (int i = 0; i < maxLen; i++)
            {
                int av = i < a.IntValues.Length ? a.IntValues[i] : 0;
                int bv = i < b.IntValues.Length ? b.IntValues[i] : 0;
                int diff = av - bv - borrow;
                if (diff < 0)
                {
                    diff += Mul;
                    borrow = 1;
                }
                else
                {
                    borrow = 0;
                }
                result[i] = diff;
            }
            var lr = new LargeInt();
            lr.IntValues = result;
            lr.TrimEnd();
            return lr;
        }

        public static LargeInt operator *(LargeInt a, int b)
        {
            if (a == null || a.IntValues == null || a.IntValues.Length == 0 || b == 0)
                return new LargeInt();
            if (b == 1) return a;
            int len = a.IntValues.Length;
            var result = new int[len + 1];
            long carry = 0;
            for (int i = 0; i < len; i++)
            {
                long prod = (long)a.IntValues[i] * b + carry;
                result[i] = (int)(prod % Mul);
                carry = prod / Mul;
            }
            if (carry > 0)
                result[len] = (int)carry;
            var lr = new LargeInt();
            lr.IntValues = result;
            lr.TrimEnd();
            return lr;
        }

        public static LargeInt operator *(LargeInt a, LargeInt b)
        {
            if (a == null || b == null) return new LargeInt();
            if (a.IntValues == null || a.IntValues.Length == 0) return new LargeInt();
            if (b.IntValues == null || b.IntValues.Length == 0) return new LargeInt();
            int len1 = a.IntValues.Length, len2 = b.IntValues.Length;
            var result = new int[len1 + len2];
            for (int i = 0; i < len1; i++)
            {
                long carry = 0;
                for (int j = 0; j < len2; j++)
                {
                    long prod = (long)a.IntValues[i] * b.IntValues[j] + result[i + j] + carry;
                    result[i + j] = (int)(prod % Mul);
                    carry = prod / Mul;
                }
                if (carry > 0)
                    result[i + len2] += (int)carry;
            }
            var lr = new LargeInt();
            lr.IntValues = result;
            lr.TrimEnd();
            return lr;
        }

        public static implicit operator LargeInt(long value)
        {
            return new LargeInt(value);
        }

        public static bool operator ==(LargeInt a, LargeInt b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(LargeInt a, LargeInt b)
        {
            return !(a == b);
        }

        public static bool operator >(LargeInt a, LargeInt b)
        {
            if (a is null) return false;
            return a.CompareTo(b) > 0;
        }

        public static bool operator <(LargeInt a, LargeInt b)
        {
            if (b is null) return false;
            return b.CompareTo(a) > 0;
        }

        public static bool operator >=(LargeInt a, LargeInt b)
        {
            if (a is null) return b is null;
            return a.CompareTo(b) >= 0;
        }

        public static bool operator <=(LargeInt a, LargeInt b)
        {
            if (b is null) return a is null;
            return b.CompareTo(a) >= 0;
        }
    }
}