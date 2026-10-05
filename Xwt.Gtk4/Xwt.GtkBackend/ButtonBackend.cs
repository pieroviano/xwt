// 
// ButtonBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc
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
	public class ButtonBackend: WidgetBackend, IButtonBackend
	{
		protected bool ignoreClickEvents;
		ImageDescription image;
		ContentPosition contentPosition;
		Gtk.Label labelWidget;
		Color? customLabelColor;
		FormattedText formattedText;
		bool isDefault;
		bool miniMode;
		bool clickedEnabled;

		protected const string DisclosureDownIcon = "pan-down-symbolic";
		protected const string DisclosureUpIcon = "pan-up-symbolic";

		public ButtonBackend ()
		{
		}

		public override void Initialize ()
		{
			Widget = new Gtk.Button ();
			InitializeButton ();
		}

		/// <summary>Common setup for the button widget, called by subclasses after creating their widget.</summary>
		protected void InitializeButton ()
		{
			base.Widget.Realized += HandleRealized;
		}

		/// <summary>The Gtk.Button (null if the backend uses a different widget, e.g. a Gtk.MenuButton).</summary>
		protected new Gtk.Button Widget {
			get { return base.Widget as Gtk.Button; }
			set { base.Widget = value; }
		}

		protected new IButtonEventSink EventSink {
			get { return (IButtonEventSink)base.EventSink; }
		}

		#region Button widget abstraction (Gtk.Button / Gtk.MenuButton)

		/// <summary>The custom content of the button.</summary>
		protected virtual Gtk.Widget ButtonContent {
			get { return Widget.Child; }
			set { Widget.Child = value; }
		}

		/// <summary>Sets a plain text label as the content of the button.</summary>
		protected virtual void SetButtonLabel (string label)
		{
			Widget.Label = label;
		}

		protected virtual bool ButtonUseUnderline {
			get { return Widget.UseUnderline; }
			set { Widget.UseUnderline = value; }
		}

		protected virtual bool ButtonHasFrame {
			get { return Widget.HasFrame; }
			set { Widget.HasFrame = value; }
		}

		/// <summary>The widget that draws the button frame and background.</summary>
		protected virtual Gtk.Widget FrameWidget {
			get { return base.Widget; }
		}

		protected virtual void ConnectClicked (bool connect)
		{
			if (connect)
				Widget.Clicked += HandleWidgetClicked;
			else
				Widget.Clicked -= HandleWidgetClicked;
		}

		#endregion

		protected override void OnSetBackgroundColor (Color color)
		{
			FrameWidget.SetBackgroundColor (color);
		}

		public virtual Color LabelColor {
			get {
				return customLabelColor.HasValue ? customLabelColor.Value : base.Widget.GetForegroundColor ();
			}
			set {
				customLabelColor = value;
				// CSS color is inherited by the label (and the image of symbolic icons)
				base.Widget.SetForegroundColor (value);
			}
		}

		public virtual bool IsDefault {
			get { return isDefault; }
			set {
				isDefault = value;
				base.Widget.ReceivesDefault = value;
				var window = GetRootWindow (base.Widget);
				if (window != null) {
					if (value)
						window.DefaultWidget = base.Widget;
					else if (window.DefaultWidget == base.Widget)
						window.DefaultWidget = null;
				}
			}
		}

		void HandleRealized (object sender, EventArgs e)
		{
			// GTK 4: the default widget belongs to the window; apply it once the button is in one
			if (isDefault) {
				var window = GetRootWindow (base.Widget);
				if (window != null)
					window.DefaultWidget = base.Widget;
			}
		}

		static Gtk.Window GetRootWindow (Gtk.Widget w)
		{
			var root = w.Root;
			return root != null ? GLib.Object.GetObject (root.Handle) as Gtk.Window : null;
		}

		public void SetContent (string label, bool useMnemonic, ImageDescription image, ContentPosition position)
		{
			SetContent (label, useMnemonic, image, position, false);
		}

		void SetContent (string label, bool useMnemonic, ImageDescription image, ContentPosition position, bool forceCustomLabel)
		{
			ButtonUseUnderline = useMnemonic;
			this.image = image;
			contentPosition = position;
			formattedText = null;
			labelWidget = null;

			if (label != null && label.Length == 0)
				label = null;

			Button b = (Button) Frontend;
			if (label != null && image.Backend == null && b.Type == ButtonType.Normal && !forceCustomLabel) {
				SetButtonLabel (label);
				return;
			}

			if (b.Type == ButtonType.Disclosure) {
				ButtonContent = Gtk.Image.NewFromIconName (DisclosureDownIcon);
				return;
			}

			Gtk.Widget contentWidget = null;

			Gtk.Widget imageWidget = null;
			if (image.Backend != null)
				imageWidget = new ImageBox (ApplicationContext, image.WithDefaultSize (Xwt.IconSize.Small));

			if (label != null) {
				labelWidget = Gtk.Label.New (null);
				labelWidget.UseUnderline = useMnemonic;
				if (useMnemonic)
					labelWidget.TextWithMnemonic = label;
				else
					labelWidget.Text = label;
				labelWidget.MnemonicWidget = base.Widget;
			}

			if (labelWidget != null && imageWidget == null) {
				contentWidget = labelWidget;
			} else if (labelWidget == null && imageWidget != null) {
				contentWidget = imageWidget;
			} else if (labelWidget != null && imageWidget != null) {
				var vertical = position == ContentPosition.Top || position == ContentPosition.Bottom;
				var box = new Gtk.Box (vertical ? Gtk.Orientation.Vertical : Gtk.Orientation.Horizontal, 3);
				box.Halign = Gtk.Align.Center;
				box.Valign = Gtk.Align.Center;
				if (position == ContentPosition.Left || position == ContentPosition.Top) {
					box.Append (imageWidget);
					box.Append (labelWidget);
				} else {
					box.Append (labelWidget);
					box.Append (imageWidget);
				}
				contentWidget = box;
			}

			if (b.Type == ButtonType.DropDown) {
				var arrow = Gtk.Image.NewFromIconName (DisclosureDownIcon);
				if (contentWidget != null) {
					var box = new Gtk.Box (Gtk.Orientation.Horizontal, 3);
					contentWidget.Hexpand = true;
					contentWidget.Halign = Gtk.Align.Start;
					if (labelWidget != null)
						labelWidget.Xalign = 0;
					box.Append (contentWidget);
					box.Append (arrow);
					contentWidget = box;
				} else
					contentWidget = arrow;
			}

			ButtonContent = contentWidget;
		}

		public void SetButtonStyle (ButtonStyle style)
		{
			switch (style) {
			case ButtonStyle.Normal:
				SetMiniMode (false);
				ButtonHasFrame = true;
				break;
			case ButtonStyle.Flat:
				SetMiniMode (false);
				ButtonHasFrame = false;
				break;
			case ButtonStyle.Borderless:
				SetMiniMode (true);
				ButtonHasFrame = false;
				break;
			}
		}

		public void SetButtonType (ButtonType type)
		{
			Button b = (Button) Frontend;
			SetContent (b.Label, b.UseMnemonic, image, b.ImagePosition);
		}

		public void SetFormattedText (FormattedText text)
		{
			// set content with a custom label, this will recreate labelWidget
			SetContent (text?.Text, ButtonUseUnderline, image, contentPosition, true);
			formattedText = text;
			if (labelWidget != null && !string.IsNullOrEmpty (text?.Text))
				labelWidget.ApplyFormattedText (text);
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is ButtonEvent) {
				switch ((ButtonEvent)eventId) {
				case ButtonEvent.Clicked:
					if (!clickedEnabled) {
						clickedEnabled = true;
						ConnectClicked (true);
					}
					break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is ButtonEvent) {
				switch ((ButtonEvent)eventId) {
				case ButtonEvent.Clicked:
					if (clickedEnabled) {
						clickedEnabled = false;
						ConnectClicked (false);
					}
					break;
				}
			}
		}

		protected void HandleWidgetClicked (object sender, EventArgs e)
		{
			if (!ignoreClickEvents) {
				ApplicationContext.InvokeUserCode (EventSink.OnClicked);
			}
		}

		/// <summary>
		/// Borderless buttons are shrunk to the size of their content (no padding, no theme minimum size).
		/// </summary>
		protected void SetMiniMode (bool miniMode)
		{
			if (this.miniMode == miniMode)
				return;
			this.miniMode = miniMode;
			var w = FrameWidget;
			w.SetStyle ("padding", miniMode ? "0" : null);
			w.SetStyle ("min-width", miniMode ? "0" : null);
			w.SetStyle ("min-height", miniMode ? "0" : null);
			w.QueueResize ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && base.Widget != null)
				base.Widget.Realized -= HandleRealized;
			labelWidget = null;
			base.Dispose (disposing);
		}
	}
}
