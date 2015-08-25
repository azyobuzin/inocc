namespace Inocc.Core
{
    public class GoPointer<T>
    {
        public GoPointer() { }

        public GoPointer(T value)
        {
            this.Value = value;
        }

        public T Value;
    }
}
