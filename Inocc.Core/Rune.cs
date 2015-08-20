namespace Inocc.Core
{
    [GoAlias(typeof(int))]
    public struct Rune
    {
        public Rune(int value)
        {
            this.Value = value;
        }

        public readonly int Value;
    }
}
