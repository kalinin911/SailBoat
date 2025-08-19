using System;
using UnityEngine;

namespace Core.HexGrid
{
    [Serializable]
    public struct HexCoordinate : IEquatable<HexCoordinate>
    {
        public int Q { get; }
        public int R { get; }
        public int S => -Q - R;

        public HexCoordinate(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static HexCoordinate FromOffset(int x, int y)
        {
            var q = x - (y - (y & 1)) / 2;
            var r = y;
            return new HexCoordinate(q, r);
        }

        public Vector2Int ToOffset()
        {
            var x = Q +(R - (R & 1)) / 2;
            var y = R;
            return new Vector2Int(x, y);
        }

        public Vector3 ToWorldPosition(float hexSize = 1f)
        {
            var x = hexSize * (Mathf.Sqrt(3f) * Q + Mathf.Sqrt(3f) / 2f * R);
            var z = hexSize * (3f / 2f * R);
            return new Vector3(x, 0f, z);
        }

        public static HexCoordinate FromWorldPosition(Vector3 worldPos, float hexSize = 1f)
        {
            var q = (2f / 3f * worldPos.x) / hexSize;
            var r = (-1f / 3f * worldPos.x + Mathf.Sqrt(3f) / 3f * worldPos.z) / hexSize;
            return HexRound(q, r);
        }

        public static HexCoordinate HexRound(float q, float r)
        {
            var s = -q - r;
            
            var rq = Mathf.Round(q);
            var rr = Mathf.Round(r);
            var rs = Mathf.Round(s);
            
            var qDiff = Mathf.Abs(rq - q);
            var rDiff = Mathf.Abs(rr - r);
            var sDiff = Mathf.Abs(rs - s);
            
            if (qDiff > rDiff && qDiff > sDiff)
                rq = -rr - rs;
            else if (rDiff > sDiff)
                rr = -rq - rs;
            
            return new HexCoordinate((int)rq, (int)rr);
        }

        public float DistanceTo(HexCoordinate other)
        {
            return (Mathf.Abs(Q - other.Q) + Mathf.Abs(Q + R - other.Q - other.R) 
                + Mathf.Abs(R - other.R)) / 2f;
        }

        public HexCoordinate[] GetNeighbors()
        {
            return new HexCoordinate[]
            {
                new HexCoordinate(Q + 1, R),
                new HexCoordinate(Q + 1, R - 1),
                new HexCoordinate(Q, R - 1),
                new HexCoordinate(Q - 1, R),
                new HexCoordinate(Q - 1, R + 1),
                new HexCoordinate(Q, R + 1)
            };
        }
        
        public bool Equals(HexCoordinate other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Q, R);
        }

        public static bool operator ==(HexCoordinate left, HexCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCoordinate left, HexCoordinate right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Hex({Q}, {R})";
        }
    }
}