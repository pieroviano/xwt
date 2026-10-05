// 
// ScrollViewBackend.cs
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


namespace Xwt.GtkBackend
{
	public class ScrollViewBackend: WidgetBackend, IScrollViewBackend
	{
		bool showBorder = true;
		
		public ScrollViewBackend ()
		{
			Widget = new Gtk.ScrolledWindow ();
		}
		
		protected new Gtk.ScrolledWindow Widget {
			get { return (Gtk.ScrolledWindow)base.Widget; }
			set { base.Widget = value; }
		}
		
		protected new IScrollViewEventSink EventSink {
			get { return (IScrollViewEventSink)base.EventSink; }
		}

		Gtk.Widget currentChild;

		public void SetChild (IWidgetBackend child)
		{
			RemoveChildPlacement (currentChild);
			currentChild = null;

			var oldChild = Widget.Child;
			if (oldChild != null) {
				// detach the content from the viewport we created, so that it can be reused
				if (oldChild is Gtk.Viewport viewport)
					viewport.Child = null;
				else if (oldChild is GtkViewPort customViewport)
					customViewport.Child = null;
				Widget.Child = null;
			}
			
			if (child != null) {
				
				var w = currentChild = GetWidgetWithPlacement (child);
				
				WidgetBackend wb = (WidgetBackend) child;
				
				if (wb.EventSink.SupportsCustomScrolling ()) {
					// the widget scrolls itself using the adjustments of the scrolled window
					var vp = new CustomViewPort (wb.EventSink);
					vp.Child = w;
					Widget.Child = vp;
				}
				else if (w is Gtk.IScrollable || w is Gtk.IScrollableImplementor)
					Widget.Child = w;
				else {
					var vp = new Gtk.Viewport (null, null);
					vp.Child = w;
					Widget.Child = vp;
				}
			}
			
			UpdateBorder ();
		}
		
		public void SetChildSize (Size s)
		{
		}
		
		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is ScrollViewEvent) {
				if (((ScrollViewEvent)eventId) == ScrollViewEvent.VisibleRectChanged) {
					Widget.Hadjustment.ValueChanged += HandleValueChanged;
					Widget.Vadjustment.ValueChanged += HandleValueChanged;
					// page size changes (resize) also change the visible rect
					Widget.Hadjustment.Changed += HandleValueChanged;
					Widget.Vadjustment.Changed += HandleValueChanged;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is ScrollViewEvent) {
				if (((ScrollViewEvent)eventId) == ScrollViewEvent.VisibleRectChanged) {
					Widget.Hadjustment.ValueChanged -= HandleValueChanged;
					Widget.Vadjustment.ValueChanged -= HandleValueChanged;
					Widget.Hadjustment.Changed -= HandleValueChanged;
					Widget.Vadjustment.Changed -= HandleValueChanged;
				}
			}
		}
		
		void HandleValueChanged (object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnVisibleRectChanged);
		}
		
		public Rectangle VisibleRect {
			get {
				double x = Widget.Hadjustment.Value;
				double y = Widget.Vadjustment.Value;
				return new Rectangle (x, y, Widget.Hadjustment.PageSize, Widget.Vadjustment.PageSize);
			}
		}
		
		public bool BorderVisible {
			get {
				return Widget.HasFrame;
			}
			set {
				showBorder = value;
				UpdateBorder ();
			}
		}
		
		void UpdateBorder ()
		{
			// GTK 4: the frame belongs to the scrolled window (no viewport/shadow types)
			Widget.HasFrame = showBorder;
		}
		
		public ScrollPolicy VerticalScrollPolicy {
			get {
				return Widget.VscrollbarPolicy.ToXwtValue ();
			}
			set {
				Widget.VscrollbarPolicy = value.ToGtkValue ();
			}
		}
		
		public ScrollPolicy HorizontalScrollPolicy {
			get {
				return Widget.HscrollbarPolicy.ToXwtValue ();
			}
			set {
				Widget.HscrollbarPolicy = value.ToGtkValue ();
			}
		}

		public IScrollControlBackend CreateVerticalScrollControl ()
		{
			return new ScrollControltBackend (Widget.Vadjustment);
		}

		public IScrollControlBackend CreateHorizontalScrollControl ()
		{
			return new ScrollControltBackend (Widget.Hadjustment);
		}
	}

	/// <summary>
	/// Hosts an Xwt widget that implements its own scrolling: the adjustments the scrolled
	/// window assigns to this GtkScrollable are handed to the Xwt widget.
	/// </summary>
	sealed class CustomViewPort: GtkViewPort
	{
		IWidgetEventSink eventSink;

		public CustomViewPort (IntPtr raw) : base (raw)
		{
		}
		
		public CustomViewPort (IWidgetEventSink eventSink)
		{
			this.eventSink = eventSink;
		}

		protected override void OnSetScrollAdjustments (Gtk.Adjustment hadj, Gtk.Adjustment vadj)
		{
			if (eventSink == null)
				return;
			var hsa = new ScrollAdjustmentBackend (hadj);
			var vsa = new ScrollAdjustmentBackend (vadj);
			
			eventSink.SetScrollAdjustments (hsa, vsa);
		}
	}
}
