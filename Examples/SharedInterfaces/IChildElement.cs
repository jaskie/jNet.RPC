using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedInterfaces
{
    public interface IChildElement
    {
        string Name { get; set; }
        double Value { get; set; }
    }
}
