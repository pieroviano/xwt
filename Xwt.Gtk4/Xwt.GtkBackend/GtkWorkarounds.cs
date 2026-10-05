//
// GtkWorkarounds.cs
//
// Authors: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (C) 2011 Xamarin Inc.
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
//


using System;
using System.Runtime.InteropServices;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Platform helpers for things GTK 4 does not expose portably (screen coordinates, native
	/// window handles, scale factors). The GTK 2/3 workarounds (image menu items, container leak,
	/// icon sets, key binding sets, quartz) have no GTK 4 counterpart and were removed.
	/// </summary>
	public static class GtkWorkarounds
	{
		public static readonly int GtkMajorVersion = (int)Gtk.Global.MajorVersion;
		public static readonly int GtkMinorVersion = (int)Gtk.Global.MinorVersion;
		public static readonly int GtkMicroVersion = (int)Gtk.Global.MicroVersion;

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void d_g_signal_stop_emission_by_name (IntPtr instance, string detailed_signal);
		static d_g_signal_stop_emission_by_name g_signal_stop_emission_by_name;

		/// <summary>Stops the emission of the signal currently being emitted on the object.</summary>
		public static void StopSignal (this GLib.Object gobject, string signalid)
		{
			if (g_signal_stop_emission_by_name == null)
				g_signal_stop_emission_by_name = GtkInterop.LoadFunction<d_g_signal_stop_emission_by_name> (NativeLib.GObject, "g_signal_stop_emission_by_name");
			g_signal_stop_emission_by_name (gobject.Handle, signalid);
		}

		public static bool TriggersContextMenu (uint button, Gdk.ModifierType state)
		{
			if (button == 3)
				return true;
			// Ctrl+click on Mac
			return Platform.IsMac && button == 1 && (state & Gdk.ModifierType.ControlMask) != 0;
		}

		public static Gdk.ModifierType GetCurrentKeyModifiers ()
		{
			var keyboard = Gdk.Display.Default?.DefaultSeat?.Keyboard;
			return keyboard != null ? keyboard.ModifierState : (Gdk.ModifierType)0;
		}

		/// <summary>Fractional scale of the surface showing the widget (1 if unrealized).</summary>
		public static double GetScaleFactor (Gtk.Widget w)
		{
			var surface = w?.Native?.Surface;
			if (surface != null)
				return surface.Scale;
			if (w != null)
				return w.ScaleFactor;
			return GetDefaultScaleFactor ();
		}

		public static double GetDefaultScaleFactor ()
		{
			var monitors = Gdk.Display.Default?.Monitors;
			if (monitors != null && monitors.NItems > 0) {
				var m = GLib.Object.GetObject (monitors.GetItem (0), true) as Gdk.Monitor;
				if (m != null)
					return m.Scale;
			}
			return 1;
		}

		/// <summary>The widget that owns the native surface the widget is drawn on (usually its window).</summary>
		public static Gtk.Widget GetNativeWidget (this Gtk.Widget widget)
		{
			var native = widget?.Native;
			if (native == null)
				return null;
			return GLib.Object.GetObject (native.Handle) as Gtk.Widget;
		}

		#region Screen coordinates

		[StructLayout (LayoutKind.Sequential)]
		struct Win32Point
		{
			public int X;
			public int Y;
		}

		[StructLayout (LayoutKind.Sequential)]
		struct Win32Rect
		{
			public int Left, Top, Right, Bottom;
		}

		[DllImport ("user32.dll")]
		static extern bool ClientToScreen (IntPtr hwnd, ref Win32Point point);

		[DllImport ("user32.dll")]
		static extern bool GetWindowRect (IntPtr hwnd, out Win32Rect rect);

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_gdk_win32_surface_get_handle (IntPtr surface);
		static d_gdk_win32_surface_get_handle gdk_win32_surface_get_handle;

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_gdk_x11_surface_get_xid (IntPtr surface);
		static d_gdk_x11_surface_get_xid gdk_x11_surface_get_xid;

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_g_type_name_from_instance (IntPtr instance);
		static d_g_type_name_from_instance g_type_name_from_instance;

		static string GetTypeName (GLib.Object o)
		{
			if (g_type_name_from_instance == null)
				g_type_name_from_instance = GtkInterop.LoadFunction<d_g_type_name_from_instance> (NativeLib.GObject, "g_type_name_from_instance");
			return Marshal.PtrToStringAnsi (g_type_name_from_instance (o.Handle));
		}

		/// <summary>
		/// The native handle of a surface: HWND on Windows, XID on X11, IntPtr.Zero elsewhere (Wayland, macOS).
		/// </summary>
		public static IntPtr GetNativeHandle (Gdk.Surface surface)
		{
			if (surface == null)
				return IntPtr.Zero;
			var typeName = GetTypeName (surface);
			if (typeName.StartsWith ("GdkWin32", StringComparison.Ordinal)) {
				if (gdk_win32_surface_get_handle == null)
					gdk_win32_surface_get_handle = GtkInterop.LoadFunction<d_gdk_win32_surface_get_handle> (NativeLib.Gtk, "gdk_win32_surface_get_handle");
				return gdk_win32_surface_get_handle != null ? gdk_win32_surface_get_handle (surface.Handle) : IntPtr.Zero;
			}
			if (typeName.StartsWith ("GdkX11", StringComparison.Ordinal)) {
				if (gdk_x11_surface_get_xid == null)
					gdk_x11_surface_get_xid = GtkInterop.LoadFunction<d_gdk_x11_surface_get_xid> (NativeLib.Gtk, "gdk_x11_surface_get_xid");
				return gdk_x11_surface_get_xid != null ? gdk_x11_surface_get_xid (surface.Handle) : IntPtr.Zero;
			}
			return IntPtr.Zero;
		}

		public static IntPtr GetGtkWindowNativeHandle (Gtk.Window window)
		{
			return GetNativeHandle (window?.Surface);
		}

		/// <summary>
		/// Screen position of the surface origin. GTK 4 does not expose global coordinates; they are
		/// queried from the windowing system where it has them (Win32). Elsewhere (Wayland, X11
		/// without a window manager query) the origin is reported as (0,0).
		/// </summary>
		public static Point GetSurfaceScreenOrigin (Gdk.Surface surface)
		{
			if (surface == null || !Platform.IsWindows)
				return Point.Zero;
			var hwnd = GetNativeHandle (surface);
			if (hwnd == IntPtr.Zero)
				return Point.Zero;
			var p = new Win32Point ();
			ClientToScreen (hwnd, ref p);
			// Win32 works in device pixels, GTK in logical ones
			var scale = surface.Scale;
			return new Point (p.X / scale, p.Y / scale);
		}

		/// <summary>Converts a point in widget coordinates to screen coordinates (best effort, see <see cref="GetSurfaceScreenOrigin"/>).</summary>
		public static Point ConvertToScreenCoordinates (Gtk.Widget widget, Point widgetCoordinates)
		{
			var nativeWidget = widget.GetNativeWidget ();
			if (nativeWidget == null)
				return Point.Zero;
			double x, y;
			if (!widget.TranslateCoordinates (nativeWidget, widgetCoordinates.X, widgetCoordinates.Y, out x, out y)) {
				x = widgetCoordinates.X;
				y = widgetCoordinates.Y;
			}
			double sx, sy;
			widget.Native.GetSurfaceTransform (out sx, out sy);
			var origin = GetSurfaceScreenOrigin (widget.Native.Surface);
			return new Point (origin.X + sx + x, origin.Y + sy + y);
		}

		#endregion
	}
}
