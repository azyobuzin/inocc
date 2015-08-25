.class public auto ansi beforefieldinit Inocc.Core.GoPointer`1<T>
    extends System.Object
    implements class Inocc.Core.IGoPointer`1<!T>
{
    .field private !T x

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        .maxstack 1
        ldarg.0
        call instance void System.Object::.ctor()
        ret
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor (!T 'value') cil managed 
    {
        .maxstack 2
        ldarg.0
        call instance void System.Object::.ctor()
        ldarg.0
        ldarg.1
        stfld !T class Inocc.Core.GoPointer`1<!T>::x
        ret
    }

    .method public final hidebysig newslot virtual instance
        native int GetAddress () cil managed
    {
        .maxstack 1
        ldarg.0
        ldflda !T class Inocc.Core.GoPointer`1<!T>::x
        ret
    }
}

//TODO: Pointer for arrays and slices
