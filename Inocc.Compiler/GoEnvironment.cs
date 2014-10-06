using System;
using System.IO;

namespace Inocc.Compiler
{
    public enum GoOS
    {
        Darwin,
        FreeBSD,
        Linux,
        NetBSD,
        OpenBSD,
        Plan9,
        Windows
    }

    public enum GoArch
    {
        AMD64,
        I386,
        ARM
    }

    public class GoEnvironment
    {
        public DirectoryInfo Path { get; set; }
        public DirectoryInfo Root { get; set; }
        public GoOS OS { get; set; }
        public GoArch Arch { get; set; }

        public static GoEnvironment CreateDefault()
        {
            GoOS os;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.WinCE:
                case PlatformID.Xbox:
                    os = GoOS.Windows;
                    break;
                case PlatformID.MacOSX:
                    os = GoOS.Darwin;
                    break;
                default:
                    os = GoOS.Linux;
                    break;
            }

            return new GoEnvironment()
            {
                Path = new DirectoryInfo(Environment.GetEnvironmentVariable("GOPATH")),
                Root = new DirectoryInfo(Environment.GetEnvironmentVariable("GOROOT")),
                OS = os,
                Arch = Environment.Is64BitOperatingSystem ? GoArch.AMD64 : GoArch.I386
            };
        }
    }
}
