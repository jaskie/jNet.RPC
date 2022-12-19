using SharedInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientApp.ViewModel
{
    class ChildElementViewModel
    {
        public ChildElementViewModel(IChildElement childElement)
        {
            ChildElement = childElement;
        }
        
        public IChildElement ChildElement { get; }


    }
}
