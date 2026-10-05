//
// PopoverBackend.cs
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
using System.Globalization;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class PopoverBackend : IPopoverBackend
	{
		/// <summary>
		/// The native popover: a <see cref="Gtk.Popover"/> anchored to the reference widget, whose
		/// background and padding are applied to its <c>contents</c> CSS node.
		/// </summary>
		public sealed class PopoverWindow : Gtk.Popover
		{
			static int instanceCounter;

			readonly string cssClass = "xwt-popover-" + (++instanceCounter).ToString (CultureInfo.InvariantCulture);
			Gtk.CssProvider cssProvider;
			Gdk.Display cssDisplay;
			Xwt.Drawing.Color? backgroundColor;
			WidgetSpacing padding;

			public PopoverWindow ()
			{
				AddCssClass (cssClass);
				Autohide = true;
				HasArrow = true;
			}

			public Xwt.Drawing.Color? BackgroundColor {
				get { return backgroundColor; }
				set {
					backgroundColor = value;
					UpdateStyle ();
				}
			}

			public Xwt.Popover.Position ArrowPosition {
				get { return Position == Gtk.PositionType.Top ? Xwt.Popover.Position.Bottom : Xwt.Popover.Position.Top; }
				// an arrow at the top means the popover is shown below the anchor
				set { Position = value == Xwt.Popover.Position.Top ? Gtk.PositionType.Bottom : Gtk.PositionType.Top; }
			}

			public WidgetSpacing Padding {
				get { return padding; }
				set {
					padding = value;
					UpdateStyle ();
				}
			}

			public Gtk.Widget Content {
				get { return Child; }
				set {
					if (Child != value)
						Child = value;
				}
			}

			void UpdateStyle ()
			{
				var display = Display ?? Gdk.Display.Default;
				if (display == null)
					return;
				if (cssProvider == null || cssDisplay != display) {
					RemoveStyle ();
					cssProvider = new Gtk.CssProvider ();
					cssDisplay = display;
					Gtk.StyleContext.AddProviderForDisplay (display, cssProvider, 800 /* GTK_STYLE_PROVIDER_PRIORITY_USER */);
				}
				var css = string.Format (CultureInfo.InvariantCulture, "popover.{0} > contents {{ padding: {1}px {2}px {3}px {4}px; {5} }}",
					cssClass, padding.Top, padding.Right, padding.Bottom, padding.Left,
					backgroundColor.HasValue ? "background-color: " + backgroundColor.Value.ToCssValue () + ";" : "");
				if (backgroundColor.HasValue)
					css += string.Format (CultureInfo.InvariantCulture, " popover.{0} > arrow {{ background-color: {1}; }}", cssClass, backgroundColor.Value.ToCssValue ());
				cssProvider.LoadFromString (css);
			}

			void RemoveStyle ()
			{
				if (cssProvider != null && cssDisplay != null)
					Gtk.StyleContext.RemoveProviderForDisplay (cssDisplay, cssProvider);
				cssProvider = null;
				cssDisplay = null;
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing)
					RemoveStyle ();
				base.Dispose (disposing);
			}
		}

		PopoverWindow popover;
		IPopoverEventSink sink;
		Popover frontend;
		ApplicationContext context;

		public Xwt.Drawing.Color BackgroundColor { get; set; }

		public PopoverWindow Popover {
			get { return popover; }
			protected set { popover = value; }
		}

		public virtual void Initialize (IPopoverEventSink sink)
		{
			this.sink = sink;
			this.BackgroundColor = Xwt.Drawing.Color.FromBytes (0xee, 0xee, 0xee, 0xf9);
			this.popover = new PopoverWindow ();
			popover.Closed += HandleClosed;
		}

		public void InitializeBackend (object frontend, ApplicationContext context)
		{
			this.frontend = (Popover) frontend;
			this.context = context;
		}

		public void EnableEvent (object eventId)
		{
		}

		public void DisableEvent (object eventId)
		{
		}

		void HandleClosed (object sender, EventArgs e)
		{
			if (context != null)
				context.InvokeUserCode (sink.OnClosed);
			else
				sink.OnClosed ();
		}

		/// <summary>
		/// Shows the popover pointing to <paramref name="positionRect"/> (relative to
		/// <paramref name="reference"/>; the whole widget if empty). GTK positions the popover and
		/// flips/shifts it to keep it on screen.
		/// </summary>
		public void Show (Xwt.Popover.Position orientation, Xwt.Widget reference, Xwt.Rectangle positionRect, Widget child)
		{
			var referenceWidget = ((IGtkWidgetBackend)Toolkit.GetBackend (reference)).Widget;
			var content = (Gtk.Widget)((WidgetBackend)Toolkit.GetBackend (child)).NativeWidget;

			if (popover.Parent != referenceWidget) {
				if (popover.Parent != null)
					popover.Unparent ();
				popover.Parent = referenceWidget;
			}

			popover.Content = content;
			popover.ArrowPosition = orientation;
			popover.BackgroundColor = BackgroundColor;
			popover.Padding = frontend.Padding;

			if (positionRect == Rectangle.Zero)
				positionRect = new Rectangle (0, 0, referenceWidget.Width, referenceWidget.Height);
			popover.PointingTo = new Gdk.Rectangle ((int)positionRect.X, (int)positionRect.Y, Math.Max (1, (int)positionRect.Width), Math.Max (1, (int)positionRect.Height));
			popover.Popup ();
		}

		public void Hide ()
		{
			popover.Popdown ();
		}

		public void Dispose ()
		{
			if (popover == null)
				return;
			popover.Closed -= HandleClosed;
			popover.Content = null;
			if (popover.Parent != null)
				popover.Unparent ();
			popover.Dispose ();
			popover = null;
		}
	}
}
