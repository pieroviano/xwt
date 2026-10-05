// 
// SelectColorDialogBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
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
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Color selection on <see cref="Gtk.ColorDialog"/>; the asynchronous GTK 4 dialog is waited for
	/// in a nested main loop.
	/// </summary>
	public class SelectColorDialogBackend: ISelectColorDialogBackend
	{
		Gtk.ColorDialog dlg;
		Color color;
		string defaultTitle;

		public SelectColorDialogBackend ()
		{
			dlg = new Gtk.ColorDialog ();
			dlg.Modal = true;
			defaultTitle = dlg.Title;
		}

		#region ISelectColorDialogBackend implementation
		public bool Run (IWindowFrameBackend parent, string title, bool supportsAlpha)
		{
			dlg.Title = !String.IsNullOrEmpty (title) ? title : defaultTitle;
			dlg.WithAlpha = supportsAlpha;

			var p = parent != null ? Toolkit.CurrentEngine.GetNativeWindow (parent) as Gtk.Window : null;
			if (p == null)
				p = MessageService.GetDefaultModalParent ();

			bool selected = false;
			Color result = color;
			var loop = new NestedLoop ();
			dlg.ChooseRgba (p, color.ToGtkRgbaValue (), null, (source, res, data) => {
				try {
					result = dlg.ChooseRgbaFinish (res).ToXwtValue ();
					selected = true;
				} catch (GLib.GException) {
					// dismissed
				} finally {
					loop.Quit ();
				}
			});
			loop.Run ();

			if (selected) {
				color = supportsAlpha ? result : result.WithAlpha (1);
				return true;
			}
			return false;
		}

		public void Dispose ()
		{
			dlg.Dispose ();
		}

		public Color Color {
			get {
				return color;
			}
			set {
				color = value;
			}
		}

		#endregion
	}
}
