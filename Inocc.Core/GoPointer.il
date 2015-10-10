.class public auto ansi beforefieldinit Inocc.Core.GoPointer`1<T>
    extends System.Object
    implements class Inocc.Core.IGoPointer`1<!T>
{
    .field private !T x

    .method public hidebysig specialname rtspecialname instance
        void .ctor () cil managed 
    {
        .maxstack 1
        ldarg.0
        call instance void System.Object::.ctor()
        ret
    }

    .method public hidebysig specialname rtspecialname instance
        void .ctor (!T 'value') cil managed 
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
        !T GetValue () cil managed
    {
        .maxstack 1
        ldarg.0
        ldfld !T class Inocc.Core.GoPointer`1<!T>::x
        ret
    }

    .method public final hidebysig newslot virtual instance
        void SetValue (!T 'value') cil managed
    {
        .maxstack 2
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
        conv.u
        ret
    }
}

.class public auto ansi beforefieldinit Inocc.Core.ArrayElementPointer`1<T>
    extends System.Object
    implements class Inocc.Core.IGoPointer`1<!T>
{
    .field private initonly !T[] 'array'
    .field private initonly int32 index

    .method public hidebysig specialname rtspecialname instance
        void .ctor (!T[] 'array', int32 index) cil managed 
    {
        .maxstack 2
        ldarg.0
        call instance void System.Object::.ctor()
        ldarg.0
        ldarg.1
        stfld !T[] class Inocc.Core.ArrayElementPointer`1<!T>::'array'
        ldarg.0
        ldarg.2
        stfld int32 class Inocc.Core.ArrayElementPointer`1<!T>::index
        ret
    }

    .method public final hidebysig newslot virtual instance
        !T GetValue () cil managed
    {
        .maxstack 2
        ldarg.0
        ldfld !T[] class Inocc.Core.ArrayElementPointer`1<!T>::'array'
        ldarg.0
        ldfld int32 class Inocc.Core.ArrayElementPointer`1<!T>::index
        ldelem !T
        ret
    }

    .method public final hidebysig newslot virtual instance
        void SetValue (!T 'value') cil managed
    {
        .maxstack 3
        ldarg.0
        ldfld !T[] class Inocc.Core.ArrayElementPointer`1<!T>::'array'
        ldarg.0
        ldfld int32 class Inocc.Core.ArrayElementPointer`1<!T>::index
        ldarg.1
        stelem !T
        ret
    }

    .method public final hidebysig newslot virtual instance
        native int GetAddress () cil managed
    {
        .maxstack 2
        ldarg.0
        ldfld !T[] class Inocc.Core.ArrayElementPointer`1<!T>::'array'
        ldarg.0
        ldfld int32 class Inocc.Core.ArrayElementPointer`1<!T>::index
        ldelema !T
        conv.u
        ret
    }
}
