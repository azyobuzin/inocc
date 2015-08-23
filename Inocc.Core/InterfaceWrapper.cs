namespace Inocc.Core
{
    public abstract class InterfaceWrapper<T>
    {
        protected InterfaceWrapper(T value)
        {
            this.Value = value;
        }

        public T Value; // readonly 検討
    }
}
