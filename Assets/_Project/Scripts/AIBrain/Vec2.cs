namespace Luddite.AIBrain
{
    /// <summary>
    /// 최소 2D 벡터. <c>UnityEngine.Vector2</c>를 쓰지 않는 이유는 하나다 —
    /// CLAUDE.md 규칙 3에 따라 <c>AIBrain/</c>은 <b>Unity 없이</b> 가짜 이벤트 시퀀스로 검증 가능해야 한다.
    /// 어댑터(<c>AIBrainRunner</c>)가 경계에서 Vector2 ↔ Vec2를 변환한다.
    /// </summary>
    public readonly struct Vec2
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        /// <summary>길이 0이면 <see cref="Zero"/>를 반환한다 (0 나눗셈 방지).</summary>
        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        /// <summary>진행 방향 기준 왼쪽 단위 벡터 (반시계 90°). LEFT/RIGHT 투영의 기준축.</summary>
        public Vec2 Left => new Vec2(-Y, X);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Magnitude;

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
