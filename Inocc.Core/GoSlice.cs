namespace Inocc.Core
{
    public struct GoSlice<T>
    {
        public GoSlice(T[] array, int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex < startIndex)
                throw new PanicException("runtime error: slice bounds out of range");

            this.Array = array;
            this.StartIndex = startIndex;
            this.EndIndex = endIndex;
        }

        public readonly T[] Array;
        public readonly int StartIndex;
        public readonly int EndIndex;

        public T this[int index]
        {
            get
            {
                var i = this.StartIndex + index;
                if (this.Array == null || index < 0 || i > this.EndIndex)
                    throw new PanicException("runtime error: index out of range");

                return this.Array[i];
            }
        }
    }
}
