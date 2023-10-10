namespace LasZip.Examples
{
    internal class PointBatchXyz
    {
        public int Count {  get; set; }

        public int[] X { get; private set; }
        public int[] Y { get; private set; }
        public int[] Z { get; private set; }

        public PointBatchXyz(int capacity)
        {
            this.Count = 0;
            this.X = new int[capacity];
            this.Y = new int[capacity];
            this.Z = new int[capacity];
        }

        public int Capacity
        {
            get { return this.X.Length; }
        }
    }
}
