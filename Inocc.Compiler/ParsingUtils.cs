using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ast = Inocc.Compiler.GoLib.Ast;
using Inocc.Core;

namespace Inocc.Compiler
{
    internal static class ParsingUtils
    {
        internal static void Write(this Stream stream, byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        internal static byte[] ReadAsUTF8(this Ast.BasicLit lit)
        {
            var v = lit.Value;
            switch (v[0])
            {
                case '"':
                    using (var ms = new MemoryStream(v.Length * 4))
                    {
                        for (var i = 0; i < v.Length; i++)
                        {
                            var c = v[i];

                            if (c == '\\')
                            {
                                switch (v[++i])
                                {
                                    case '0':
                                    case '1':
                                    case '2':
                                    case '3':
                                    case '4':
                                    case '5':
                                    case '6':
                                    case '7':
                                        ms.WriteByte(Convert.ToByte(v.Substring(i, 3), 8));
                                        i += 2;
                                        break;
                                    case 'x':
                                        ms.WriteByte(Convert.ToByte(v.Substring(i + 1, 2), 16));
                                        i += 2;
                                        break;
                                    case 'u':
                                        ms.Write(Encoding.UTF8.GetBytes(char.ConvertFromUtf32(
                                            Convert.ToInt32(v.Substring(i + 1, 4), 16))));
                                        i += 4;
                                        break;
                                    case 'U':
                                        ms.Write(Encoding.UTF8.GetBytes(char.ConvertFromUtf32(
                                            Convert.ToInt32(v.Substring(i + 1, 8), 16))));
                                        i += 8;
                                        break;
                                    case 'a':
                                        ms.WriteByte((byte)'\a');
                                        break;
                                    case 'b':
                                        ms.WriteByte((byte)'\b');
                                        break;
                                    case 'f':
                                        ms.WriteByte((byte)'\f');
                                        break;
                                    case 'n':
                                        ms.WriteByte((byte)'\n');
                                        break;
                                    case 'r':
                                        ms.WriteByte((byte)'\r');
                                        break;
                                    case 't':
                                        ms.WriteByte((byte)'\t');
                                        break;
                                    case 'v':
                                        ms.WriteByte((byte)'\v');
                                        break;
                                    case '\\':
                                        ms.WriteByte((byte)'\\');
                                        break;
                                    case '\'':
                                        ms.WriteByte((byte)'\'');
                                        break;
                                    case '"':
                                        ms.WriteByte((byte)'"');
                                        break;
                                    default:
                                        throw new FormatException("Invalid character: \\" + v[i]);
                                }
                            }
                            else if (char.IsHighSurrogate(c))
                            {
                                ms.Write(Encoding.UTF8.GetBytes(new[] { c, v[++i] }));
                            }
                            else
                            {
                                ms.Write(Encoding.UTF8.GetBytes(new[] { c }));
                            }
                        }
                        return ms.ToArray();
                    }
                case '`':
                    return Encoding.UTF8.GetBytes(ReadAsString(lit));
            }
            throw new FormatException();
        }

        internal static string ReadAsString(this Ast.BasicLit lit)
        {
            var v = lit.Value;
            switch (v[0])
            {
                case '"':
                    return Encoding.UTF8.GetString(ReadAsUTF8(lit));
                case '`':
                    return v.Substring(1, v.Length - 2);
            }
            throw new FormatException();
        }
    }
}
