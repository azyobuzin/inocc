namespace Inocc.Core
{
    public class GoPointer<T>
    {
        public GoPointer(T value)
        {
            this.Set(value);
        }

        public T Value;
        public bool IsNotNil;

        public void ThrowIfIsNil()
        {
            if (!this.IsNotNil)
            {
                // runtime.errorString
                throw new PanicException("runtime error: invalid memory address or nil pointer dereference");
            }
        }

        public void Set(T value)
        {
            this.IsNotNil = false;
            this.Value = value;
        }
    }
}
