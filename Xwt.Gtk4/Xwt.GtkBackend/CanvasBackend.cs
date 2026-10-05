// 
// CanvasBackend.cs
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
using System.Collections.Generic;
using Xwt.Backends;
using System.Linq;

using Xwt.CairoBackend;

namespace Xwt.GtkBackend
{
	public class CanvasBackend: WidgetBackend, ICanvasBackend
	{
		public CanvasBackend ()
		{
		}

		public override void Initialize ()
		{
			Widget = new CustomCanvas ();
			Widget.Backend = this;
			Widget.EventSink = EventSink;
		}

		new CustomCanvas Widget {
			get { return (CustomCanvas)base.Widget; }
			set { base.Widget = value; }
		}

		protected new ICanvasEventSink EventSink {
			get { return (ICanvasEventSink)base.EventSink; }
		}

		public void QueueDraw ()
		{
			Widget.QueueDraw ();
		}

		public void QueueDraw (Rectangle rect)
		{
			// GTK 4: no partial invalidation, the whole canvas is redrawn
			Widget.QueueDraw ();
		}

		public Rectangle Bounds {
			get {
				return new Rectangle (0, 0, Widget.Width, Widget.Height);
			}
		}

		public void AddChild (IWidgetBackend widget, Rectangle bounds)
		{
			var w = ((IGtkWidgetBackend)widget).Widget;
			Widget.Add (w);
			Widget.SetAllocation (w, bounds);
		}

		public void RemoveChild (IWidgetBackend widget)
		{
			var w = ((IGtkWidgetBackend)widget).Widget;
			Widget.RemoveChild (w);
		}

		public void SetChildBounds (IWidgetBackend widget, Rectangle bounds)
		{
			var w = ((IGtkWidgetBackend)widget).Widget;
			Widget.SetAllocation (w, bounds);
		}
	}

	/// <summary>
	/// The canvas widget: draws the Xwt content with Cairo in its snapshot, then the child
	/// widgets on top at the positions given by the frontend.
	/// </summary>
	class CustomCanvas: ContainerWidget
	{
		public CanvasBackend Backend;
		public ICanvasEventSink EventSink;
		Dictionary<Gtk.Widget, Rectangle> children = new Dictionary<Gtk.Widget, Rectangle> ();
		int lastWidth = -1, lastHeight = -1;
		bool measuring;

		public CustomCanvas ()
		{
		}

		public void SetAllocation (Gtk.Widget w, Rectangle rect)
		{
			children [w] = rect;
			QueueResize ();
		}

		public void Add (Gtk.Widget widget)
		{
			children [widget] = new Rectangle (0, 0, 0, 0);
			AddChild (widget);
		}

		public override void RemoveChild (Gtk.Widget widget)
		{
			children.Remove (widget);
			base.RemoveChild (widget);
		}

		public override void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			Rectangle r;
			if (children.TryGetValue (oldWidget, out r)) {
				children.Remove (oldWidget);
				children [newWidget] = r;
			}
			base.ReplaceChild (oldWidget, newWidget);
		}

		protected override void OnUnrealized ()
		{
			base.OnUnrealized ();
			lastWidth = lastHeight = -1;
		}

		protected override Gtk.SizeRequestMode OnGetRequestMode ()
		{
			// fixed width-height relation
			return Gtk.SizeRequestMode.ConstantSize;
		}

		protected override void OnMeasure (Gtk.Orientation orientation, int for_size, out int minimum, out int natural, out int minimum_baseline, out int natural_baseline)
		{
			minimum_baseline = natural_baseline = -1;
			bool horizontal = orientation == Gtk.Orientation.Horizontal;

			// Ask the frontend first (it may override OnGetPreferredSize). The default frontend
			// implementation comes back here through WidgetBackend.GetPreferredSize, and that
			// nested call answers with the children extents.
			if (!measuring && Backend?.Frontend != null) {
				measuring = true;
				try {
					var size = ((IWidgetSurface)Backend.Frontend).GetPreferredSize (false);
					minimum = natural = (int) Math.Max (0, horizontal ? size.Width : size.Height);
					return;
				} finally {
					measuring = false;
				}
			}

			int request = horizontal ? WidthRequest : HeightRequest;
			if (request > 0) {
				minimum = natural = request;
				return;
			}
			double min = Backend?.Frontend != null ? (horizontal ? Backend.Frontend.MinWidth : Backend.Frontend.MinHeight) : 0;
			double size2 = Math.Max (min, 0);
			foreach (var cr in children.Where (c => c.Key.Visible)) {
				var r = cr.Value;
				size2 = Math.Max (size2, horizontal ? r.X + r.Width : r.Y + r.Height);
			}
			minimum = natural = (int) size2;
		}

		protected override void OnSizeAllocate (int width, int height, int baseline)
		{
			if (width != lastWidth || height != lastHeight) {
				lastWidth = width;
				lastHeight = height;
				((IWidgetSurface)Backend.Frontend).Reallocate ();
			}
			foreach (var cr in children.ToArray ()) {
				if (!cr.Key.Visible)
					continue;
				var r = cr.Value;
				var w = (int) Math.Max (r.Width, 0);
				var h = (int) Math.Max (r.Height, 0);
				// GTK 4 requires a measure before allocating
				cr.Key.Measure (Gtk.Orientation.Horizontal, -1, out _, out _, out _, out _);
				cr.Key.Measure (Gtk.Orientation.Vertical, w, out _, out _, out _, out _);
				cr.Key.SizeAllocate (new Gdk.Rectangle ((int)r.X, (int)r.Y, w, h), -1);
			}
		}

		protected override void OnSnapshot (Gtk.Snapshot snapshot)
		{
			int width = Width, height = Height;
			if (width > 0 && height > 0 && Backend != null) {
				var bounds = Graphene.Rect.Alloc ();
				bounds.Init (0, 0, width, height);
				var cr = snapshot.AppendCairo (bounds);
				var context = new CairoContextBackend (Util.GetScaleFactor (this)) { Context = cr };
				// the snapshot offset is part of the initial CTM: make it the Xwt origin, so that
				// the CTM seen by the frontend starts as identity
				var m = cr.Matrix;
				context.Origin.X = m.X0;
				context.Origin.Y = m.Y0;
				// disposes the context, which completes the Cairo render node
				OnDraw (new Rectangle (0, 0, width, height), context);
			}
			// children are drawn above the canvas content
			base.OnSnapshot (snapshot);
		}

		protected void OnDraw (Rectangle dirtyRect, CairoContextBackend context)
		{
			Backend.ApplicationContext.InvokeUserCode (delegate {
				using (context) {
					EventSink.OnDraw (context, dirtyRect);
				}
			});
		}

		public CairoContextBackend CreateContext ()
		{
			// GTK 4 widgets cannot be drawn outside the snapshot: an offscreen context is returned
			// (useful for measuring), matching the old behaviour of an unrealized canvas.
			CairoContextBackend ctx = new CairoContextBackend (Util.GetScaleFactor (this));
			Cairo.Surface sf = new Cairo.ImageSurface (Cairo.Format.Argb32, 1, 1);
			ctx.Context = new Cairo.Context (sf);
			ctx.TempSurface = sf;
			return ctx;
		}
	}
}
