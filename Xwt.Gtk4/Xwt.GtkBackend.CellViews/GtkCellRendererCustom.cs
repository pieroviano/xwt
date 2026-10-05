//
// GtkCellRendererCustom.cs
//
// Author:
//       Vsevolod Kukol <v.kukol@rubologic.de>
//
// Copyright (c) 2014 Vsevolod Kukol
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

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Base class of the managed cell renderers (canvas and image cells).
	/// </summary>
	public abstract class GtkCellRendererCustom : Gtk.CellRenderer
	{
		/// <summary>Text describing the cell (Xwt AccessibleFields.Label).</summary>
		[GLib.Property ("text")]
		public string AccessibleText { get; set; }

		public GtkCellRendererCustom ()
		{
			// Default padding of the native renderers (Gtk.CellRendererText): the tree view derives
			// the cell area from the background area using it. The preferred size reported by the
			// subclasses does not include it.
			SetPadding (1, 0);
			SetAlignment (0.0f, 0.0f);
		}

		protected override Gtk.SizeRequestMode OnGetRequestMode ()
		{
			return Gtk.SizeRequestMode.HeightForWidth;
		}
	}
}
