namespace GroomingTool2.Core
{
    public readonly struct MyBrushData
    {
        public readonly int X;
        public readonly int Y;
        public readonly float Influence;

        public MyBrushData(int x, int y, float influence)
        {
            X = x;
            Y = y;
            Influence = influence;
        }
    }
}



