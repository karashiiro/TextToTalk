using System;
using System.Collections.Generic;
using System.Linq;

namespace TextToTalk.UI
{
    public class WindowManager : IDisposable, IImmediateModeWindow
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
                window.Draw(ref visible);
            }
        }

        /// <summary>
        /// Draws all visible windows assigned to this <see cref="WindowManager"/> if it is visible.
        /// </summary>
        /// <param name="visible">Whether or not this <see cref="WindowManager"/> is visible.</param>
        public void Draw(ref bool visible)
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
        /// Shows the <see cref="IImmediateModeWindow"/> specified by the type parameter. Throws an exception if the
        /// window has not been installed into this instance.
        /// </summary>
        /// <typeparam name="TWindow">The window type.</typeparam>
        public void ShowWindow<TWindow>() where TWindow : IImmediateModeWindow
        {
            var windowInfo = this.windows.First(w => w.Instance is TWindow);
            windowInfo.Visible = true;
        }

        /// <summary>
        /// Toggles the <see cref="IImmediateModeWindow"/> specified by the type parameter. Throws an exception if the
        /// window has not been installed into this instance.
        /// </summary>
        /// <typeparam name="TWindow">The window type.</typeparam>
        public void ToggleWindow<TWindow>() where TWindow : IImmediateModeWindow
        {
            var windowInfo = this.windows.First(w => w.Instance is TWindow);
            windowInfo.Visible = !windowInfo.Visible;
        }

        /// <summary>
        /// Installs an <see cref="IImmediateModeWindow"/> into this instance and hydrates it with any applicable service implementations.
        /// Injected services are identified with public properties on the <see cref="IImmediateModeWindow"/>; however, a missing implementation
        /// will not assume that a service is required, and will instead leave it <c>null</c>. Likewise, if the constructor of the window
        /// assigns to a public property, this will be detected and no services will be injected into those populated properties.
        /// </summary>
        /// <typeparam name="TWindow">The window type.</typeparam>
        /// <param name="initiallyVisible">Whether or not the window should begin visible.</param>
        public void InstallWindow<TWindow>(bool initiallyVisible) where TWindow : IImmediateModeWindow
        {
            var instance = Activator.CreateInstance(typeof(TWindow));
            foreach (var property in instance.GetType().GetProperties())
            {
                if (property.GetValue(instance) != null)
                {
                    continue;
                }

                var fulfillingService = this.services.FirstOrDefault(s => property.PropertyType.IsInstanceOfType(s));
                property.SetValue(instance, fulfillingService);
            }

            this.windows.Add(new WindowInfo
            {
                Instance = (IImmediateModeWindow)instance,
                Visible = initiallyVisible,
            });
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
            public IImmediateModeWindow Instance { get; set; }

            public bool Visible { get; set; }
        }
    }
}