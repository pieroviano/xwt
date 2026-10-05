//
// SelectFontDialogBackend.cs
//
// Author:
//       Vsevolod Kukol <sevo@sevo.org>
//
// Copyright (c) 2015 Vsevolod Kukol
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
	/// Font selection on <see cref="Gtk.FontDialog"/>; the asynchronous GTK 4 dialog is waited for
	/// in a nested main loop.
	/// </summary>
	public class SelectFontDialogBackend: ISelectFontDialogBackend
	{
		readonly Gtk.FontDialog dlg;
		string defaultTitle;
		ApplicationContext context;

		public Font SelectedFont {
			get;
			set;
		}

		public string Title {
			get;
			set;
		}

		/// <summary>GTK 4: GtkFontDialog has no settable preview text; the value is only remembered.</summary>
		public string PreviewText {
			get;
			set;
		}

		public SelectFontDialogBackend ()
		{
			dlg = new Gtk.FontDialog ();
			dlg.Modal = true;
			defaultTitle = dlg.Title;
		}

		public void Initialize (ApplicationContext actx)
		{
			context = actx;
		}

		public bool Run (IWindowFrameBackend parent)
		{
			dlg.Title = !String.IsNullOrEmpty (Title) ? Title : defaultTitle;

			Pango.FontDescription initial = null;
			if (SelectedFont != null)
				initial = Toolkit.GetBackend (SelectedFont) as Pango.FontDescription ?? Pango.FontDescription.FromString (SelectedFont.ToString ());

			var p = parent != null ? context.Toolkit.GetNativeWindow (parent) as Gtk.Window : null;
			if (p == null)
				p = MessageService.GetDefaultModalParent ();

			Pango.FontDescription result = null;
			var loop = new NestedLoop ();
			dlg.ChooseFont (p, initial, null, (source, res, data) => {
				try {
					result = dlg.ChooseFontFinish (res);
				} catch (GLib.GException) {
					// dismissed
				} finally {
					loop.Quit ();
				}
			});
			loop.Run ();

			if (result != null) {
				SelectedFont = Font.FromName (result.ToString ());
				return true;
			}
			return false;
		}

		public void Dispose ()
		{
			dlg.Dispose ();
		}
	}
}
