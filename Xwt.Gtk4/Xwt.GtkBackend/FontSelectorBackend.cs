//
// FontSelectorBackend.cs
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

namespace Xwt.GtkBackend
{
	public class FontSelectorBackend: WidgetBackend, IFontSelectorBackend
	{
		public FontSelectorBackend ()
		{
		}

		public override void Initialize ()
		{
			// GTK 4: GtkFontSelection is gone; GtkFontChooserWidget (deprecated 4.10, present) is the inline selector
			Widget = new Gtk.FontChooserWidget ();
		}

		protected new Gtk.FontChooserWidget Widget {
			get { return (Gtk.FontChooserWidget)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IFontSelectorEventSink EventSink {
			get { return (IFontSelectorEventSink)base.EventSink; }
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is FontSelectorEvent) {
				switch ((FontSelectorEvent)eventId) {
					case FontSelectorEvent.FontChanged: Widget.AddNotification ("font", HandleFontChanged); break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is FontSelectorEvent) {
				switch ((FontSelectorEvent)eventId) {
					case FontSelectorEvent.FontChanged: Widget.RemoveNotification ("font", HandleFontChanged); break;
				}
			}
		}

		void HandleFontChanged (object sender, GLib.NotifyArgs e)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnFontChanged);
		}

		public Xwt.Drawing.Font SelectedFont {
			get {
				return Xwt.Drawing.Font.FromName (Widget.Font);
			}
			set {
				Widget.Font = value.ToString ();
			}
		}

		public string PreviewText {
			get { return Widget.PreviewText; }
			set { Widget.PreviewText = value; }
		}
	}
}
