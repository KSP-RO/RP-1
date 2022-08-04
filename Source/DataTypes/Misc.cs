using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RP0.DataTypes
{
    public class Boxed<T> where T : struct
    {
        public T value;
        public Boxed(T val) { value = val; }
        public Boxed() { }
    }
}
