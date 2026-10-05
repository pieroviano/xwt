//
// ColorPickerBackend.cs
//
// Author:
//       Vsevolod Kukol <sevo@sevo.org>
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
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class ColorPickerBackend: WidgetBackend, IColorPickerBackend
	{
		bool settingColor;

		public ColorPickerBackend ()
		{
		}

		public override void Initialize ()
		{
			// GTK 4: GtkColorButton is replaced by GtkColorDialogButton + GtkColorDialog
			Widget = new Gtk.ColorDialogButton (new Gtk.ColorDialog { WithAlpha = false });
		}

		protected new Gtk.ColorDialogButton Widget {
			get { return (Gtk.ColorDialogButton)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IColorPickerEventSink EventSink {
			get { return (IColorPickerEventSink)base.EventSink; }
		}

		public Xwt.Drawing.Color Color {
			get {
				return Widget.Rgba.ToXwtValue ();
			}
			set {
				// only user choices raise ColorChanged (as GtkColorButton::color-set did)
				settingColor = true;
				try {
					Widget.Rgba = value.ToGtkRgbaValue ();
				} finally {
					settingColor = false;
				}
			}
		}

		public bool SupportsAlpha {
			get {
				return Widget.Dialog.WithAlpha;
			}
			set {
				Widget.Dialog.WithAlpha = value;
			}
		}

		public string Title {
			get {
				return Widget.Dialog.Title;
			}
			set {
				Widget.Dialog.Title = value;
			}
		}

		public void SetButtonStyle (ButtonStyle style)
		{
			// the visible button is the internal GtkButton child of the GtkColorDialogButton
			var button = Widget.FirstChild ?? Widget;
			if (style == ButtonStyle.Normal)
				button.RemoveCssClass ("flat");
			else
				button.AddCssClass ("flat");
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is ColorPickerEvent) {
				switch ((ColorPickerEvent)eventId) {
					case ColorPickerEvent.ColorChanged: Widget.AddNotification ("rgba", HandleColorSet); break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is ColorPickerEvent) {
				switch ((ColorPickerEvent)eventId) {
					case ColorPickerEvent.ColorChanged: Widget.RemoveNotification ("rgba", HandleColorSet); break;
				}
			}
		}

		void HandleColorSet (object sender, GLib.NotifyArgs e)
		{
			if (settingColor)
				return;
			ApplicationContext.InvokeUserCode (EventSink.OnColorChanged);
		}
	}
}
