// 
// WindowFrameBa.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//       Konrad M. Kruczynski <kkruczynski@antmicro.com>
// 
// Copyright (c) 2011 Xamarin Inc
// Copyright (c) 2016 Antmicro Ltd
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.IO;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class WindowFrameBackend: IWindowFrameBackend
	{
		Gtk.Window window;
		IWindowFrameEventSink eventSink;
		WindowFrame frontend;
		Size requestedSize;
		Gdk.Surface layoutSurface;
		bool boundsEventEnabled, closeEventEnabled, shownEventEnabled, hiddenEventEnabled;
		bool showInTaskbar = true;

		public WindowFrameBackend ()
		{
		}

		public Gtk.Window Window {
			get { return window; }
			set {
				if (window != null) {
					window.CloseRequest -= HandleCloseRequest;
					window.Realized -= HandleRealized;
					window.Unrealized -= HandleUnrealized;
					window.Shown -= HandleShown;
					window.Hidden -= HandleHidden;
					window.RemoveNotification ("default-width", HandleDefaultSizeChanged);
					window.RemoveNotification ("default-height", HandleDefaultSizeChanged);
					DetachSurface ();
				}
				window = value;
				if (window != null) {
					window.CloseRequest += HandleCloseRequest;
					window.Realized += HandleRealized;
					window.Unrealized += HandleUnrealized;
					window.Shown += HandleShown;
					window.Hidden += HandleHidden;
					window.AddNotification ("default-width", HandleDefaultSizeChanged);
					window.AddNotification ("default-height", HandleDefaultSizeChanged);
					if (window.Surface != null)
						AttachSurface (window.Surface);
				}
			}
		}

		object IWindowFrameBackend.Window {
			get { return Window; }
		}

		/// <summary>HWND on Windows, XID on X11; IntPtr.Zero elsewhere or before the window is realized.</summary>
		public IntPtr NativeHandle {
			get { return GtkWorkarounds.GetGtkWindowNativeHandle (Window); }
		}

		protected WindowFrame Frontend {
			get { return frontend; }
		}

		public ApplicationContext ApplicationContext {
			get;
			private set;
		}

		void IBackend.InitializeBackend (object frontend, ApplicationContext context)
		{
			this.frontend = (WindowFrame) frontend;
			ApplicationContext = context;
		}

		public virtual void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			throw new NotSupportedException ();
		}

		#region IWindowFrameBackend implementation
		void IWindowFrameBackend.Initialize (IWindowFrameEventSink eventSink)
		{
			this.eventSink = eventSink;
			Initialize ();
		}

		public virtual void Initialize ()
		{
		}

		public virtual void Dispose ()
		{
			var w = Window;
			if (w != null) {
				Window = null;
				w.Destroy ();
			}
		}

		public IWindowFrameEventSink EventSink {
			get { return eventSink; }
		}

		public void Move (double x, double y)
		{
			// GTK 4: toplevels can't be positioned by the application, the window manager places them.
		}

		public virtual void SetSize (double width, double height)
		{
			if (width <= 0 || height <= 0) {
				var current = Bounds;
				if (width <= 0)
					width = current.Width;
				if (height <= 0)
					height = current.Height;
			}
			requestedSize = new Size (width, height);
			// GTK 4: the default size is the only size request a toplevel has; a mapped window is resized to it.
			Window.SetDefaultSize ((int)width, (int)height);
		}

		/// <summary>
		/// Window bounds. GTK 4 has no window positioning: the location is the best the platform can
		/// report (<see cref="GtkWorkarounds.GetSurfaceScreenOrigin"/>, (0,0) where unavailable),
		/// and setting the bounds applies the size only.
		/// </summary>
		public Rectangle Bounds {
			get {
				var surface = Window.Surface;
				var origin = GtkWorkarounds.GetSurfaceScreenOrigin (surface);
				double w, h;
				if (surface != null && surface.Width > 0) {
					w = surface.Width;
					h = surface.Height;
				} else {
					int dw, dh;
					Window.GetDefaultSize (out dw, out dh);
					w = dw > 0 ? dw : Math.Max (requestedSize.Width, Window.Width);
					h = dh > 0 ? dh : Math.Max (requestedSize.Height, Window.Height);
				}
				return new Rectangle (origin.X, origin.Y, w, h);
			}
			set {
				// GTK 4: the location can't be set (no window positioning), only the size.
				requestedSize = value.Size;
				Window.SetDefaultSize ((int)value.Width, (int)value.Height);
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnBoundsChanged (Bounds);
				});
			}
		}

		public Size RequestedSize {
			get { return requestedSize; }
		}

		string IWindowFrameBackend.Name {
			get { return Window.Name; }
			set { Window.Name = value; }
		}

		bool IWindowFrameBackend.Visible {
			get { return window.Visible; }
			set { window.Visible = value; }
		}

		bool IWindowFrameBackend.Sensitive {
			get { return window.Sensitive; }
			set { window.Sensitive = value; }
		}

		double IWindowFrameBackend.Opacity {
			get { return Window.Opacity; }
			set { Window.Opacity = value; }
		}

		bool IWindowFrameBackend.HasFocus {
			get { return Window.IsActive; }
		}

		string IWindowFrameBackend.Title {
			get { return Window.Title; }
			set { Window.Title = value; }
		}

		bool IWindowFrameBackend.Decorated {
			get { return Window.Decorated; }
			set { Window.Decorated = value; }
		}

		bool IWindowFrameBackend.ShowInTaskbar {
			// GTK 4: there is no skip-taskbar hint; the value is only remembered.
			get { return showInTaskbar; }
			set { showInTaskbar = value; }
		}

		void IWindowFrameBackend.SetTransientFor (IWindowFrameBackend window)
		{
			Window.TransientFor = window != null ? ApplicationContext.Toolkit.GetNativeWindow (window) as Gtk.Window : null;
		}

		public bool Resizable {
			get { return Window.Resizable; }
			set { Window.Resizable = value; }
		}

		bool IWindowFrameBackend.FullScreen {
			get { return Window.IsFullscreen; }
			set {
				if (value)
					Window.Fullscreen ();
				else
					Window.Unfullscreen ();
			}
		}

		/// <summary>The <see cref="Gdk.Monitor"/> showing the window (the first monitor while it is unrealized).</summary>
		object IWindowFrameBackend.Screen {
			get {
				var display = Window.Display ?? Gdk.Display.Default;
				if (Window.Surface != null) {
					var m = display.GetMonitorAtSurface (Window.Surface);
					if (m != null)
						return m;
				}
				return GtkDesktopBackend.GetMonitor (display, 0);
			}
		}

		static int iconCounter;
		static string iconDirectory;

		/// <summary>
		/// GTK 4 windows take an icon name only. Images that are not theme icons are saved as
		/// unthemed PNG icons in a private icon-theme search path and referenced by name.
		/// </summary>
		public void SetIcon (ImageDescription icon)
		{
			var img = icon.Backend as GtkImage;
			if (img == null)
				return;
			if (!string.IsNullOrEmpty (img.IconName)) {
				Window.IconName = img.IconName;
				return;
			}
			try {
				double size = Math.Max (128, Math.Max (icon.Size.Width, icon.Size.Height));
				var pix = img.GetBestFrame (ApplicationContext, 1, size, size, false);
				if (pix == null)
					return;
				var display = Window.Display ?? Gdk.Display.Default;
				if (iconDirectory == null) {
					var dir = Path.Combine (Path.GetTempPath (), "xwt-gtk4-icons-" + System.Diagnostics.Process.GetCurrentProcess ().Id);
					Directory.CreateDirectory (dir);
					Gtk.IconTheme.GetForDisplay (display).AddSearchPath (dir);
					iconDirectory = dir;
				}
				var name = "xwt-window-icon-" + (++iconCounter);
				File.WriteAllBytes (Path.Combine (iconDirectory, name + ".png"), pix.SaveToBuffer ("png"));
				Window.IconName = name;
			} catch (Exception ex) {
				Console.WriteLine (ex);
			}
		}
		#endregion

		public virtual void EnableEvent (object ev)
		{
			if (ev is WindowFrameEvent) {
				switch ((WindowFrameEvent)ev) {
				case WindowFrameEvent.BoundsChanged:
					boundsEventEnabled = true; break;
				case WindowFrameEvent.Closed:
				case WindowFrameEvent.CloseRequested:
					closeEventEnabled = true; break;
				case WindowFrameEvent.Shown:
					shownEventEnabled = true; break;
				case WindowFrameEvent.Hidden:
					hiddenEventEnabled = true; break;
				}
			}
		}

		public virtual void DisableEvent (object ev)
		{
			if (ev is WindowFrameEvent) {
				switch ((WindowFrameEvent)ev) {
				case WindowFrameEvent.BoundsChanged:
					boundsEventEnabled = false; break;
				case WindowFrameEvent.Shown:
					shownEventEnabled = false; break;
				case WindowFrameEvent.Hidden:
					hiddenEventEnabled = false; break;
				}
			}
		}

		void HandleRealized (object sender, EventArgs e)
		{
			AttachSurface (Window.Surface);
		}

		void HandleUnrealized (object sender, EventArgs e)
		{
			DetachSurface ();
		}

		void AttachSurface (Gdk.Surface surface)
		{
			DetachSurface ();
			layoutSurface = surface;
			if (layoutSurface != null)
				layoutSurface.Layout += HandleSurfaceLayout;
		}

		void DetachSurface ()
		{
			if (layoutSurface != null) {
				layoutSurface.Layout -= HandleSurfaceLayout;
				layoutSurface = null;
			}
		}

		void HandleSurfaceLayout (object o, Gdk.LayoutArgs args)
		{
			NotifyBoundsChanged ();
		}

		void HandleDefaultSizeChanged (object o, GLib.NotifyArgs args)
		{
			// once mapped, size changes are reported by the surface layout signal
			if (layoutSurface == null)
				NotifyBoundsChanged ();
		}

		void NotifyBoundsChanged ()
		{
			if (!boundsEventEnabled || eventSink == null)
				return;
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnBoundsChanged (Bounds);
			});
		}

		void HandleHidden (object sender, EventArgs e)
		{
			if (hiddenEventEnabled && eventSink != null)
				ApplicationContext.InvokeUserCode (EventSink.OnHidden);
		}

		void HandleShown (object sender, EventArgs e)
		{
			if (shownEventEnabled && eventSink != null)
				ApplicationContext.InvokeUserCode (EventSink.OnShown);
		}

		void HandleCloseRequest (object o, Gtk.CloseRequestArgs args)
		{
			args.RetVal = OnCloseRequest ();
		}

		/// <summary>
		/// Handles the close-request of the window (the user closing it).
		/// Returns <c>true</c> to keep the window open.
		/// </summary>
		protected virtual bool OnCloseRequest ()
		{
			if (!closeEventEnabled || eventSink == null)
				return false;
			return !PerformClose (true);
		}

		internal bool PerformClose (bool userClose)
		{
			bool close = false;
			ApplicationContext.InvokeUserCode(delegate {
				close = EventSink.OnCloseRequested ();
			});
			if (close) {
				if (!userClose)
					Window.Visible = false;
				ApplicationContext.InvokeUserCode(EventSink.OnClosed);
			}
			return close;
		}

		public void Present ()
		{
			Window.Present ();
		}

		public virtual bool Close ()
		{
			return PerformClose (false);
		}

		public virtual void GetMetrics (out Size minSize, out Size decorationSize)
		{
			minSize = decorationSize = Size.Zero;
		}
	}
}
