using System;
using System.Collections.Generic;
using System.Linq;

namespace TextToTalk.UI
{
    public class WindowManager : ImmediateModeWindow, IDisposable
    {
        private readonly IList<WindowInfo> windows;
        private readonly IList<object> services;

        public WindowManager()
        {
            this.windows = new List<WindowInfo>();
            this.services = new List<object>();
        }

        /// <summary>
        /// Draws all visible windows assigned to this <see cref="WindowManager"/>.
        /// </summary>
        public void Draw()
        {
            foreach (var windowInfo in this.windows)
            {
                var window = windowInfo.Instance;
                var visible = windowInfo.Visible;

                if (!visible)
                {
                    continue;
                }

                window.Draw(ref visible);

                if (windowInfo.Visible != visible)
                {
                    windowInfo.Visible = visible;
                }
            }
        }

        /// <summary>
        /// Draws all visible windows assigned to this <see cref="WindowManager"/> if it is visible.
        /// </summary>
        /// <param name="visible">Whether or not this <see cref="WindowManager"/> is visible.</param>
        public override void Draw(ref bool visible)
        {
            if (visible)
            {
                Draw();
            }
        }

        /// <summary>
        /// Installs a service implementation into the service collection of this <see cref="WindowManager"/>.
        /// This instance will become responsible for disposing the service.
        /// </summary>
        /// <typeparam name="TServiceImplementation">The service type.</typeparam>
        /// <param name="instance">The service instance.</param>
        public void InstallService<TServiceImplementation>(TServiceImplementation instance)
        {
            this.services.Add(instance);
        }

        /// <summary>
        /// Shows the <see cref="ImmediateModeWindow"/> specified by the type parameter. Throws an exception if the
        /// window has not been installed into this instance.
        /// </summary>
        /// <typeparam name="TWindow">The window type.</typeparam>
        public void ShowWindow<TWindow>() where TWindow : ImmediateModeWindow
        {
            var windowInfo = this.windows.First(w => w.Instance is TWindow);
            windowInfo.Visible = true;
        }

        /// <summary>
        /// Toggles the <see cref="ImmediateModeWindow"/> specified by the type parameter. Throws an exception if the
        /// window has not been installed into this instance.
        /// </summary>
        /// <typeparam name="TWindow">The window type.</typeparam>
        public void ToggleWindow<TWindow>() where TWindow : ImmediateModeWindow
        {
            var windowInfo = this.windows.First(w => w.Instance is TWindow);
            windowInfo.Visible = !windowInfo.Visible;
        }

        /// <summary>
        /// Installs an <see cref="ImmediateModeWindow"/> into this instance and hydrates it with any applicable service implementations.
        /// Injected services are identified with public properties on the <see cref="ImmediateModeWindow"/>; however, a missing implementation
        /// will not assume that a service is required, and will instead leave it <c>null</c>. Likewise, if the constructor of the window
        /// assigns to a public property, this will be detected and no services will be injected into those populated properties.
        /// </summary>
        /// <typeparam name="TWindow">The window type.</typeparam>
        /// <param name="initiallyVisible">Whether or not the window should begin visible.</param>
        public void InstallWindow<TWindow>(bool initiallyVisible) where TWindow : ImmediateModeWindow
        {
            var instance = (ImmediateModeWindow)Activator.CreateInstance(typeof(TWindow));
            foreach (var property in instance.GetType().GetProperties())
            {
                if (property.GetValue(instance) != null)
                {
                    continue;
                }

                var fulfillingService = this.services.FirstOrDefault(s => property.PropertyType.IsInstanceOfType(s));
                property.SetValue(instance, fulfillingService);
            }

            instance.ForeignWindowOpenRequested += OnWindowOpenRequested;
            instance.ForeignWindowReferenceRequested += OnWindowReferenceRequested;

            this.windows.Add(new WindowInfo
            {
                Instance = instance,
                Visible = initiallyVisible,
            });
        }

        /// <summary>
        /// Callback method called when an installed <see cref="ImmediateModeWindow"/> requests that another window be opened.
        /// </summary>
        /// <param name="windowType">The type of the window to be opened.</param>
        private void OnWindowOpenRequested(Type windowType)
        {
            var windowInfo = this.windows.First(w => windowType.IsInstanceOfType(w.Instance));
            windowInfo.Visible = true;
        }

        /// <summary>
        /// Callback method called when an installed <see cref="ImmediateModeWindow"/> requests that another window be returned.
        /// </summary>
        /// <param name="windowType">The type of the window to be returned.</param>
        public object OnWindowReferenceRequested(Type windowType)
        {
            return this.windows.First(w => windowType.IsInstanceOfType(w.Instance)).Instance;
        }

        /// <summary>
        /// Calls <see cref="IDisposable.Dispose"/> on any services installed into this instance that implement <see cref="IDisposable"/>.
        /// </summary>
        public void Dispose()
        {
            foreach (var service in this.services)
            {
                if (service is IDisposable disposableService)
                {
                    disposableService.Dispose();
                }
            }
        }

        private class WindowInfo
        {
            public ImmediateModeWindow Instance { get; set; }

            public bool Visible { get; set; }
        }
    }
}