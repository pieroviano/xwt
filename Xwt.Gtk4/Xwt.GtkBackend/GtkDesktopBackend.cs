//
// GtkDesktopBackend.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc.
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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Desktop information. Screens are represented by their <see cref="Gdk.Monitor"/>.
	/// </summary>
	public class GtkDesktopBackend: DesktopBackend
	{
		GLib.IListModel monitors;

		#region implemented abstract members of DesktopBackend

		public GtkDesktopBackend ()
		{
			monitors = Gdk.Display.Default?.Monitors;
			if (monitors != null) {
				monitors.ItemsChanged += delegate {
					OnScreensChanged ();
				};
			}
		}

		internal static Gdk.Monitor GetMonitor (Gdk.Display display, uint index)
		{
			var list = (display ?? Gdk.Display.Default)?.Monitors;
			if (list == null || index >= list.NItems)
				return null;
			return GLib.Object.GetObject (list.GetItem (index), true) as Gdk.Monitor;
		}

		static IEnumerable<Gdk.Monitor> GetMonitors ()
		{
			var display = Gdk.Display.Default;
			var list = display?.Monitors;
			if (list == null)
				yield break;
			for (uint n = 0; n < list.NItems; n++) {
				var m = GetMonitor (display, n);
				if (m != null)
					yield return m;
			}
		}

		[StructLayout (LayoutKind.Sequential)]
		struct Win32Point
		{
			public int X;
			public int Y;
		}

		[DllImport ("user32.dll")]
		static extern bool GetCursorPos (out Win32Point point);

		/// <summary>
		/// GTK 4 has no global pointer position. On Windows it is read from the system; elsewhere it
		/// is the position over the active window (translated with its best-effort screen origin),
		/// or (0,0) when the pointer is not over a window of this application.
		/// </summary>
		public override Point GetMouseLocation ()
		{
			if (Platform.IsWindows) {
				Win32Point p;
				if (GetCursorPos (out p)) {
					var scale = GtkWorkarounds.GetDefaultScaleFactor ();
					return new Point (p.X / scale, p.Y / scale);
				}
			}
			var pointer = Gdk.Display.Default?.DefaultSeat?.Pointer;
			if (pointer == null)
				return Point.Zero;
			foreach (var w in Gtk.Window.ListToplevels ()) {
				var surface = w.Surface;
				if (surface == null || !w.IsActive)
					continue;
				double x, y;
				Gdk.ModifierType mask;
				if (surface.GetDevicePosition (pointer, out x, out y, out mask)) {
					var origin = GtkWorkarounds.GetSurfaceScreenOrigin (surface);
					return new Point (origin.X + x, origin.Y + y);
				}
			}
			return Point.Zero;
		}

		public override IEnumerable<object> GetScreens ()
		{
			foreach (var m in GetMonitors ())
				yield return m;
		}

		/// <summary>
		/// GTK 4 has no primary monitor: the primary screen is the one containing the origin
		/// (the Windows convention), or the first monitor.
		/// </summary>
		public override bool IsPrimaryScreen (object backend)
		{
			Gdk.Monitor primary = null;
			foreach (var m in GetMonitors ()) {
				var g = m.Geometry;
				if (g.X <= 0 && g.Y <= 0 && g.X + g.Width > 0 && g.Y + g.Height > 0) {
					primary = m;
					break;
				}
				if (primary == null)
					primary = m;
			}
			return primary != null && primary.Equals (backend);
		}

		public override Rectangle GetScreenBounds (object backend)
		{
			var r = ((Gdk.Monitor)backend).Geometry;
			return new Rectangle (r.X, r.Y, r.Width, r.Height);
		}

		/// <summary>GTK 4 does not expose the monitor work area; the full geometry is returned.</summary>
		public override Rectangle GetScreenVisibleBounds (object backend)
		{
			return GetScreenBounds (backend);
		}

		public override string GetScreenDeviceName (object backend)
		{
			var m = (Gdk.Monitor)backend;
			return m.Connector ?? m.Model ?? m.Description ?? string.Empty;
		}

		public override double GetScaleFactor (object backend)
		{
			var m = (Gdk.Monitor)backend;
			var scale = m.Scale;
			return scale > 0 ? scale : m.ScaleFactor;
		}

		#endregion

		public override void OpenUrl (string url)
		{
			var launcher = new Gtk.UriLauncher (url);
			launcher.Launch (MessageService.GetDefaultModalParent (), null, (source, res, data) => {
				try {
					launcher.LaunchFinish (res);
				} catch (GLib.GException ex) {
					Console.WriteLine (ex);
				}
			});
		}

		public override void OpenFile (string filename)
		{
			Launch (filename);
		}

		public override void OpenFolder (string folderPath)
		{
			Launch (folderPath);
		}

		static void Launch (string path)
		{
			var launcher = new Gtk.FileLauncher (GLib.FileFactory.NewForPath (path));
			launcher.Launch (MessageService.GetDefaultModalParent (), null, (source, res, data) => {
				try {
					launcher.LaunchFinish (res);
				} catch (GLib.GException ex) {
					Console.WriteLine (ex);
				}
			});
		}

		/// <summary>The themed icon of the file's content type.</summary>
		public override object GetFileIcon (string fileName)
		{
			string contentType;
			if (Directory.Exists (fileName))
				contentType = "inode/directory";
			else {
				bool uncertain;
				contentType = GLib.ContentType.Guess (fileName, null, out uncertain);
			}
			if (string.IsNullOrEmpty (contentType))
				return null;
			var icon = GLib.ContentType.GetIcon (contentType);
			var themed = icon != null ? GLib.Object.GetObject (icon.Handle) as GLib.ThemedIcon : null;
			var display = Gdk.Display.Default;
			if (themed == null || display == null)
				return null;
			var theme = Gtk.IconTheme.GetForDisplay (display);
			foreach (var name in themed.Names) {
				if (theme.HasIcon (name))
					return new GtkImage (name);
			}
			return null;
		}
	}
}
