using System;

namespace TextToTalk.UI
{
    public abstract class ImmediateModeWindow
    {
        public Action<Type> ForeignWindowOpenRequested { get; set; }

        public Func<Type, object> ForeignWindowReferenceRequested { get; set; }

        public abstract void Draw(ref bool visible);

        protected void OpenWindow<TWindow>() where TWindow : ImmediateModeWindow
        {
            ForeignWindowOpenRequested?.Invoke(typeof(TWindow));
        }

        protected TWindow GetWindow<TWindow>() where TWindow : ImmediateModeWindow
        {
            return (TWindow)ForeignWindowReferenceRequested?.Invoke(typeof(TWindow));
        }
    }
}