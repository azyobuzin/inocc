# ポインタの扱いについて
ポインタは IGoPointer<T> を実装したクラスによって表されます。

最初から用意してある IGoPointer の実装は
* GoPointer - ローカル変数の入れもの
* ArrayElementPointer - .NET の配列とインデックス

があり、そのほかはコンパイル時に自動生成されます。

# 自動生成の例
```go
func main() {
	a := x { 1 }
	b := &a.n
	*b = 2
}

type x struct {
	n int
}
```
に対して出力されるコードを C# で模すと
```csharp
static void main() {
    var a = new GoPointer<x>(new x { n = 1 });
    // b はエスケープしないので GoPointer で囲わない
    var b = new bPtr(a);
    b.SetValue(2);
}

struct x {
    internal int n;
}

class bPtr : IGoPointer<int> {
    internal bPtr(IGoPointer<x> root) {
        this.root = root;
    }

    private readonly IGoPointer<x> root;

    public int GetValue() {
        return this.root.GetAddress()->n;
    }

    public void SetValue(int value) {
        this.root.GetAddress()->n = value;
    }

    public IntPtr GetAddress() {
        return &this.root.GetAddress()->n;
    }
}
```
のようになります。
