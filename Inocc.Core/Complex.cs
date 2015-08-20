using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inocc.Core
{
    public struct Complex64
    {
        public Complex64(float r, float i)
        {
            this.Real = r;
            this.Imaginary = i;
        }

        public readonly float Real;
        public readonly float Imaginary;
    }

    public struct Complex128
    {
        public Complex128(double r, double i)
        {
            this.Real = r;
            this.Imaginary = i;
        }

        public readonly double Real;
        public readonly double Imaginary;
    }
}
