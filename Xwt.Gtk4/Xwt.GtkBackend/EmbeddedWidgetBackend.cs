//
// EmbeddedWidgetBackend.cs
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
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class EmbeddedWidgetBackend: WidgetBackend, IEmbeddedWidgetBackend
	{
		public EmbeddedWidgetBackend ()
		{
		}

		public void SetContent (object nativeWidget, bool reparent)
		{
			Gtk.Widget gtkWidget = nativeWidget as Gtk.Widget;
			if (gtkWidget == null && nativeWidget is Xwt.Widget xwtWidget)
				gtkWidget = GetWidget ((IWidgetBackend)Toolkit.GetBackend (xwtWidget));

			// GTK 4 cannot host foreign native views (no NSView or HWND/WPF embedding)
			if (gtkWidget == null)
				throw new NotSupportedException ("GTK 4 can only embed Gtk.Widget or Xwt.Widget instances, not " + (nativeWidget?.GetType ().FullName ?? "null"));

			// GTK 4 widgets have a single parent: detach it from its current one when asked to
			if (reparent && gtkWidget.Parent != null)
				GtkEngine.RemoveChild (gtkWidget);
			Widget = gtkWidget;
		}
	}
}
