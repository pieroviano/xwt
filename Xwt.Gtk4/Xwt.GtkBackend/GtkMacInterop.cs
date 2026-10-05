//
// GtkMacInterop.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//
// Copyright (c) 2012 Xamarin, Inc.
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
using System.Linq;

namespace Xwt.GtkBackend
{
	public class GtkMacInterop
	{
		/// <summary>
		/// GTK 4: embedding an NSView in a GTK widget is not possible (GtkNSView was a GTK 2 quartz
		/// widget and the GTK 4 macOS backend exposes no equivalent).
		/// </summary>
		public static Gtk.Widget NSViewToGtkWidget (object view)
		{
			throw new NotSupportedException ("Embedding an NSView is not supported by GTK 4");
		}

		/// <summary>Finds the GTK window whose native handle is the Handle of <paramref name="window"/>.</summary>
		public static Gtk.Window GetGtkWindow (object window)
		{
			if (window == null)
				return null;

			var prop = window.GetType ().GetProperty ("Handle");
			var handle = prop?.GetValue (window, null);
			if (handle is IntPtr) {
				var toplevels = Gtk.Window.ListToplevels ();
				return toplevels.FirstOrDefault (w => w.Surface != null && GtkWorkarounds.GetGtkWindowNativeHandle (w) == (IntPtr)handle);
			}
			return null;
		}
	}
}
