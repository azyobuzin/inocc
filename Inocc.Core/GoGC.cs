using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Inocc.Core
{
    public unsafe static class GoGC
    {
        private const string msvcrt = "msvcrt";

        [DllImport(msvcrt, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr calloc(IntPtr num, UIntPtr size);

        [DllImport(msvcrt, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free(IntPtr ptr);

        private struct PointerInfo
        {
            public PointerInfo(Type type, int num)
            {
                this.Type = type;
                this.Count = num;
            }

            public Type Type;
            public int Count;
        }

        private static readonly Dictionary<IntPtr, PointerInfo> managedPointers = new Dictionary<IntPtr, PointerInfo>();

        public static void* Alloc(Type type, int num, uint size)
        {
            if (num < 0) throw new ArgumentOutOfRangeException();
            var ptr = calloc(new IntPtr(num), new UIntPtr(size));
            managedPointers.Add(ptr,new PointerInfo( type, num));
            return ptr.ToPointer();
        }

        public static void* Alloc(Type type, int num, int size)
        {
            return Alloc(type, num, checked((uint)size));
        }

        public static void* Alloc(Type type, uint size)
        {
            return Alloc(type, 1, size);
        }

        public static void* Alloc(Type type, int size)
        {
            return Alloc(type, 1, checked((uint)size));
        }

        private static readonly HashSet<Type> staticTypes = new HashSet<Type>();
        private static readonly HashSet<object> roots = new HashSet<object>();

        public static void AddRoot(Type staticType)
        {
            staticTypes.Add(staticType);
        }

        public static void AddRoot(object obj)
        {
            roots.Add(obj);
        }

        public static void Collect()
        {

        }

        private static void MarkPointer(IntPtr ptr, HashSet<IntPtr> marked)
        {
            if (!marked.Add(ptr)) return;

            PointerInfo info;
            if (managedPointers.TryGetValue(ptr, out info))
            {
                if (info.Type.IsPointer || info.Type == typeof(IntPtr) || info.Type == typeof(UIntPtr))
                {
                    MarkPointer(Marshal.PtrToStructure<IntPtr>(ptr), marked);
                }
                else
                {
                    var obj = Marshal.PtrToStructure(ptr, info.Type);
                    switch (Type.GetTypeCode(info.Type))
                    {
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.SByte:
                            MarkPointer(new IntPtr(Convert.ToInt64(obj)), marked);
                            break;
                        case TypeCode.Byte:
                        case TypeCode.Char:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            MarkPointer(new IntPtr(unchecked((long)Convert.ToUInt64(obj))), marked);
                            break;
                        default:
                            Mark(obj, marked);
                            break;
                    }
                }
            }
        }

        private static readonly Dictionary<Type, Action<HashSet<IntPtr>>> staticMarkers = new Dictionary<Type, Action<HashSet<IntPtr>>>();

        private static void MarkStatic(Type target, HashSet<IntPtr> marked)
        {
            Action<HashSet<IntPtr>> marker;
            if (staticMarkers.TryGetValue(target, out marker))
            {
                marker(marked);
            }
            else
            {
                var method = new DynamicMethod("Mark_" + target.Name, typeof(void), new[] { typeof(HashSet<IntPtr>) });
                var il = method.GetILGenerator();
                
            }
        }

        private static void Mark(object target, HashSet<IntPtr> marked)
        {

        }
    }
}
