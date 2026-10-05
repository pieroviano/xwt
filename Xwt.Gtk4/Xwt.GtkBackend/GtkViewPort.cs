//
// GtkViewPort.cs
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
	/// Backend widget with a single child that gets the whole allocation and determines the size.
	/// </summary>
	public class SingleChildContainer: ContainerWidget
	{
		Gtk.Widget child;

		public SingleChildContainer ()
		{
		}

		public SingleChildContainer (IntPtr raw) : base (raw)
		{
		}

		public Gtk.Widget Child {
			get { return child; }
			set {
				if (child == value)
					return;
				if (child != null)
					RemoveChild (child);
				child = value;
				if (value != null)
					AddChild (value);
			}
		}

		public override void RemoveChild (Gtk.Widget widget)
		{
			if (widget == child)
				child = null;
			base.RemoveChild (widget);
		}

		public override void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			if (oldWidget == child)
				child = newWidget;
			base.ReplaceChild (oldWidget, newWidget);
		}

		protected override Gtk.SizeRequestMode OnGetRequestMode ()
		{
			return child != null ? child.RequestMode : Gtk.SizeRequestMode.ConstantSize;
		}

		protected override void OnMeasure (Gtk.Orientation orientation, int for_size, out int minimum, out int natural, out int minimum_baseline, out int natural_baseline)
		{
			minimum_baseline = natural_baseline = -1;
			if (child != null && child.Visible)
				child.Measure (orientation, for_size, out minimum, out natural, out _, out _);
			else
				minimum = natural = 0;
		}

		protected override void OnSizeAllocate (int width, int height, int baseline)
		{
			if (child != null && child.Visible)
				child.SizeAllocate (new Gdk.Rectangle (0, 0, width, height), -1);
		}
	}

	/// <summary>
	/// Single-child widget implementing GtkScrollable: a GtkScrolledWindow hands it its
	/// adjustments (through the "hadjustment"/"vadjustment" interface properties) and the child
	/// is allocated the whole viewport, being responsible for its own scrolling.
	/// </summary>
	public class GtkViewPort: SingleChildContainer, Gtk.IScrollableImplementor
	{
		Gtk.Adjustment hadjustment;
		Gtk.Adjustment vadjustment;

		public GtkViewPort ()
		{
		}

		public GtkViewPort (IntPtr raw) : base (raw)
		{
		}

		public Gtk.Adjustment Hadjustment {
			get {
				return hadjustment;
			}
			set {
				hadjustment = value;
				if (hadjustment != null && vadjustment != null)
					OnSetScrollAdjustments (hadjustment, vadjustment);
			}
		}

		public Gtk.Adjustment Vadjustment {
			get {
				return vadjustment;
			}
			set {
				vadjustment = value;
				if (hadjustment != null && vadjustment != null)
					OnSetScrollAdjustments (hadjustment, vadjustment);
			}
		}

		public Gtk.ScrollablePolicy HscrollPolicy { get; set; }

		public Gtk.ScrollablePolicy VscrollPolicy { get; set; }

		public bool GetBorder (out Gtk.Border border)
		{
			border = new Gtk.Border ();
			return false;
		}

		protected virtual void OnSetScrollAdjustments (Gtk.Adjustment hadj, Gtk.Adjustment vadj)
		{
		}
	}
}
