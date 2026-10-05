// 
// FrameBackend.cs
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
	/// <summary>
	/// FrameType.WidgetBox: a Gtk.Frame (padding applied by an inner border-less <see cref="HeaderBox"/>);
	/// FrameType.Custom: a <see cref="HeaderBox"/> drawing the border widths/colour itself.
	/// </summary>
	public class FrameBackend: WidgetBackend, IFrameBackend
	{
		HeaderBox paddingBox;
		string label;
		Color? borderColor;
		Gtk.Widget content;

		public FrameBackend ()
		{
			Widget = new Gtk.Frame (null);
		}

		protected new IFrameEventSink EventSink {
			get { return (IFrameEventSink)base.EventSink; }
		}

		Gtk.Frame GtkFrame {
			get { return Widget as Gtk.Frame; }
		}

		HeaderBox CustomBox {
			get { return Widget as HeaderBox; }
		}

		/// <summary>Detaches the content widget from whatever currently holds it.</summary>
		void DetachContent ()
		{
			if (content == null)
				return;
			if (paddingBox != null && paddingBox.Child == content)
				paddingBox.Child = null;
			else if (CustomBox != null && CustomBox.Child == content)
				CustomBox.Child = null;
			else if (GtkFrame != null && GtkFrame.Child == content)
				GtkFrame.Child = null;
		}

		/// <summary>Puts the content widget in the current frame widget.</summary>
		void AttachContent ()
		{
			if (CustomBox != null) {
				CustomBox.Child = content;
			} else if (paddingBox != null) {
				paddingBox.Child = content;
				if (GtkFrame.Child != paddingBox)
					GtkFrame.Child = paddingBox;
			} else
				GtkFrame.Child = content;
		}

		public override void UpdateChildPlacement (IWidgetBackend childBackend)
		{
			base.UpdateChildPlacement (childBackend);
			// the placement wrapper may have been added or removed around the content
			content = CustomBox != null ? CustomBox.Child : paddingBox != null ? paddingBox.Child : GtkFrame.Child;
		}

		#region IFrameBackend implementation

		public void SetContent (IWidgetBackend child)
		{
			DetachContent ();
			RemoveChildPlacement (content);
			content = child != null ? GetWidgetWithPlacement (child) : null;
			AttachContent ();
		}

		public void SetFrameType (FrameType type)
		{
			Frame f = (Frame) Frontend;

			switch (type) {
			case FrameType.Custom:
				if (CustomBox == null) {
					DetachContent ();
					if (paddingBox != null) {
						if (GtkFrame != null)
							GtkFrame.Child = null;
						paddingBox = null;
					}
					HeaderBox box = new HeaderBox ();
					box.BackgroundColor = UsingCustomBackgroundColor ? (Color?)BackgroundColor : null;
					#pragma warning disable CS0618 // Type or member is obsolete
					box.SetMargins ((int)f.BorderWidthTop, (int)f.BorderWidthBottom, (int)f.BorderWidthLeft, (int)f.BorderWidthRight);
					#pragma warning restore CS0618 // Type or member is obsolete
					box.SetPadding ((int)f.Padding.Top, (int)f.Padding.Bottom, (int)f.Padding.Left, (int)f.Padding.Right);
					if (borderColor != null)
						box.SetBorderColor (borderColor.Value);
					Widget = box;
					AttachContent ();
				}
				break;
			case FrameType.WidgetBox:
				if (GtkFrame == null) {
					DetachContent ();
					var gf = new Gtk.Frame (string.IsNullOrEmpty (label) ? null : label);
					Widget = gf;
					if (f.Padding.HorizontalSpacing != 0 || f.Padding.VerticalSpacing != 0) {
						paddingBox = new HeaderBox ();
						paddingBox.SetPadding ((int)f.Padding.Top, (int)f.Padding.Bottom, (int)f.Padding.Left, (int)f.Padding.Right);
					}
					AttachContent ();
				}
				break;
			}
		}

		public void SetBorderSize (double left, double right, double top, double bottom)
		{
			CustomBox?.SetMargins ((int)top, (int)bottom, (int)left, (int)right);
		}

		public void SetPadding (double left, double right, double top, double bottom)
		{
			if (CustomBox != null) {
				CustomBox.SetPadding ((int)top, (int)bottom, (int)left, (int)right);
				return;
			}

			if (left == 0 && right == 0 && top == 0 && bottom == 0 && paddingBox == null)
				return;

			if (paddingBox == null) {
				DetachContent ();
				paddingBox = new HeaderBox ();
				AttachContent ();
			}
			paddingBox.SetPadding ((int)top, (int)bottom, (int)left, (int)right);
		}

		public Color BorderColor {
			get {
				if (borderColor == null) {
					Gdk.RGBA c;
					if (Widget.StyleContext.LookupColor ("borders", out c))
						return c.ToXwtValue ();
					return new Color (0.5, 0.5, 0.5);
				}
				return borderColor.Value;
			}
			set {
				borderColor = value;
				CustomBox?.SetBorderColor (value);
			}
		}

		public override Color BackgroundColor {
			get {
				return base.BackgroundColor;
			}
			set {
				base.BackgroundColor = value;
				if (CustomBox != null)
					CustomBox.BackgroundColor = value;
			}
		}

		protected override void OnSetBackgroundColor (Color color)
		{
			// the custom frame paints its own background
			if (CustomBox == null)
				base.OnSetBackgroundColor (color);
		}

		public string Label {
			get {
				return label;
			}
			set {
				label = value;
				if (GtkFrame != null)
					GtkFrame.Label = string.IsNullOrEmpty (value) ? null : value;
			}
		}
		#endregion
	}
}
