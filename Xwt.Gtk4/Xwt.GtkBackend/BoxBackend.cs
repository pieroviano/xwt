// 
// BoxBackend.cs
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
using System.Linq;
using Xwt.Backends;
using Xwt;
using System.Collections.Generic;

namespace Xwt.GtkBackend
{
	partial class BoxBackend: WidgetBackend, IBoxBackend
	{
		public BoxBackend ()
		{
			Widget = new CustomContainer () { Backend = this };
		}

		new CustomContainer Widget {
			get { return (CustomContainer)base.Widget; }
			set { base.Widget = value; }
		}

		public void Add (IWidgetBackend widget)
		{
			WidgetBackend wb = (WidgetBackend) widget;
			Widget.Add (wb.Frontend, GetWidget (widget));
		}

		public void Remove (IWidgetBackend widget)
		{
			Widget.RemoveChild (GetWidget (widget));
		}

		public void SetAllocation (IWidgetBackend[] widgets, Rectangle[] rects)
		{
			bool changed = false;
			for (int n=0; n<widgets.Length; n++) {
				var w = GetWidget (widgets[n]);
				if (Widget.SetAllocation (w, rects[n]))
					changed = true;
			}
			if (changed && !Widget.IsReallocating)
				Widget.QueueAllocate ();
		}

		public override Size Size {
			get { return Widget.AllocatedSize; }
		}
	}

	/// <summary>
	/// Base for backend widgets that own children: children are attached with
	/// <c>child.Parent = this</c> and detached with <c>Unparent</c> (GTK 4 has no GtkContainer).
	/// The default GtkWidget snapshot draws the children.
	/// </summary>
	public abstract class ContainerWidget: Gtk.Widget, IGtkContainer
	{
		protected ContainerWidget ()
		{
		}

		protected ContainerWidget (IntPtr raw): base (raw)
		{
		}

		public IEnumerable<Gtk.Widget> Children {
			get { return this.GetChildren (); }
		}

		protected void AddChild (Gtk.Widget child)
		{
			child.Parent = this;
			QueueResize ();
		}

		public virtual void RemoveChild (Gtk.Widget child)
		{
			if (child.Parent == this) {
				child.Unparent ();
				QueueResize ();
			}
		}

		public virtual void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			newWidget.InsertAfter (this, oldWidget);
			oldWidget.Unparent ();
			QueueResize ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && Handle != IntPtr.Zero) {
				foreach (var c in Children.ToArray ())
					c.Unparent ();
			}
			base.Dispose (disposing);
		}
	}

	/// <summary>
	/// Container whose layout is computed by the Xwt frontend (Box, Table...).
	/// </summary>
	partial class CustomContainer: ContainerWidget
	{
		public BoxBackend Backend;
		public bool IsReallocating;
		Dictionary<Gtk.Widget, WidgetData> children = new Dictionary<Gtk.Widget, WidgetData> ();
		Size allocatedSize;

		struct WidgetData
		{
			public Rectangle Rect;
			public Widget Widget;
		}

		public Size AllocatedSize {
			get { return allocatedSize; }
		}

		public override void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			WidgetData r = children [oldWidget];
			children.Remove (oldWidget);
			children [newWidget] = r;
			base.ReplaceChild (oldWidget, newWidget);
		}

		public bool SetAllocation (Gtk.Widget w, Rectangle rect)
		{
			WidgetData r;
			children.TryGetValue (w, out r);
			if (r.Rect != rect) {
				r.Rect = rect;
				children [w] = r;
				return true;
			} else
				return false;
		}

		public void Add (Widget w, Gtk.Widget gw)
		{
			children.Add (gw, new WidgetData () { Widget = w, Rect = new Rectangle (0,0,0,0) });
			AddChild (gw);
		}

		public override void RemoveChild (Gtk.Widget widget)
		{
			children.Remove (widget);
			base.RemoveChild (widget);
		}

		protected Size GetPreferredSize (SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
			return Backend.Frontend.Surface.GetPreferredSize (widthConstraint, heightConstraint, true);
		}

		protected override Gtk.SizeRequestMode OnGetRequestMode ()
		{
			return Gtk.SizeRequestMode.HeightForWidth;
		}

		protected override void OnMeasure (Gtk.Orientation orientation, int for_size, out int minimum, out int natural, out int minimum_baseline, out int natural_baseline)
		{
			Size size;
			if (orientation == Gtk.Orientation.Horizontal) {
				size = GetPreferredSize (SizeConstraint.Unconstrained, for_size >= 0 ? SizeConstraint.WithSize (for_size) : SizeConstraint.Unconstrained);
				minimum = natural = Math.Max ((int)size.Width, WidthRequest);
			} else {
				size = GetPreferredSize (for_size >= 0 ? SizeConstraint.WithSize (for_size) : SizeConstraint.Unconstrained, SizeConstraint.Unconstrained);
				minimum = natural = Math.Max ((int)size.Height, HeightRequest);
			}
			minimum_baseline = natural_baseline = -1;
		}

		protected override void OnSizeAllocate (int width, int height, int baseline)
		{
			allocatedSize = new Size (width, height);
			try {
				IsReallocating = true;
				((IWidgetSurface)Backend.Frontend).Reallocate ();
			} finally {
				IsReallocating = false;
			}
			foreach (var cr in children.ToArray ()) {
				var r = cr.Value.Rect;
				if (cr.Key.Visible)
					cr.Key.SizeAllocate (new Gdk.Rectangle ((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height), -1);
			}
		}
	}
}
