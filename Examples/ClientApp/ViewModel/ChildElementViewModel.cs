using SharedInterfaces;

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
