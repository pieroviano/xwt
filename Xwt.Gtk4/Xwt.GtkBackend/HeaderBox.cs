// 
// HeaderBox.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
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
using Xwt.Drawing;
using Xwt.CairoBackend;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Single-child container drawing a border of configurable widths/colour and an optional background,
	/// with the child allocated inside the border and the padding.
	/// </summary>
	class HeaderBox: ContainerWidget
	{
		Gtk.Widget child;
		int topMargin;
		int bottomMargin;
		int leftMargin;
		int rightMargin;

		int topPadding;
		int bottomPadding;
		int leftPadding;
		int rightPadding;

		Color? color;
		Color? backgroundColor;
		bool gradientBackground;

		public HeaderBox ()
		{
		}

		public HeaderBox (IntPtr p): base (p)
		{
		}

		public HeaderBox (int topMargin, int bottomMargin, int leftMargin, int rightMargin)
		{
			SetMargins (topMargin, bottomMargin, leftMargin, rightMargin);
		}

		public Gtk.Widget Child {
			get { return child; }
			set {
				if (child == value)
					return;
				if (child != null && child.Parent == this)
					base.RemoveChild (child);
				child = value;
				if (child != null)
					AddChild (child);
			}
		}

		public override void RemoveChild (Gtk.Widget w)
		{
			if (w == child)
				child = null;
			base.RemoveChild (w);
		}

		public override void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			if (oldWidget == child)
				child = newWidget;
			base.ReplaceChild (oldWidget, newWidget);
		}

		public void SetBorderColor (Color color)
		{
			this.color = color;
			QueueDraw ();
		}

		public void SetMargins (int topMargin, int bottomMargin, int leftMargin, int rightMargin)
		{
			this.topMargin = topMargin;
			this.bottomMargin = bottomMargin;
			this.leftMargin = leftMargin;
			this.rightMargin = rightMargin;
			QueueResize ();
		}

		public void SetPadding (int topPadding, int bottomPadding, int leftPadding, int rightPadding)
		{
			this.topPadding = topPadding;
			this.bottomPadding = bottomPadding;
			this.leftPadding = leftPadding;
			this.rightPadding = rightPadding;
			QueueResize ();
		}

		public bool GradientBackround {
			get { return gradientBackground; }
			set { gradientBackground = value; QueueDraw (); }
		}

		public Color? BackgroundColor {
			get { return backgroundColor; }
			set { backgroundColor = value; QueueDraw (); }
		}

		int HorizontalExtra {
			get { return leftMargin + rightMargin + leftPadding + rightPadding; }
		}

		int VerticalExtra {
			get { return topMargin + bottomMargin + topPadding + bottomPadding; }
		}

		protected override Gtk.SizeRequestMode OnGetRequestMode ()
		{
			return child != null ? child.RequestMode : Gtk.SizeRequestMode.ConstantSize;
		}

		protected override void OnMeasure (Gtk.Orientation orientation, int for_size, out int minimum, out int natural, out int minimum_baseline, out int natural_baseline)
		{
			minimum_baseline = natural_baseline = -1;
			int extra = orientation == Gtk.Orientation.Horizontal ? HorizontalExtra : VerticalExtra;
			if (child != null && child.Visible) {
				int otherExtra = orientation == Gtk.Orientation.Horizontal ? VerticalExtra : HorizontalExtra;
				int childForSize = for_size >= 0 ? Math.Max (0, for_size - otherExtra) : -1;
				int mb, nb;
				child.Measure (orientation, childForSize, out minimum, out natural, out mb, out nb);
				minimum += extra;
				natural += extra;
			} else {
				minimum = natural = extra;
			}
		}

		protected override void OnSizeAllocate (int width, int height, int baseline)
		{
			if (child == null || !child.Visible)
				return;
			int x = 0, y = 0;
			if (width > HorizontalExtra) {
				x = leftMargin + leftPadding;
				width -= HorizontalExtra;
			}
			if (height > VerticalExtra) {
				y = topMargin + topPadding;
				height -= VerticalExtra;
			}
			child.SizeAllocate (new Gdk.Rectangle (x, y, width, height), -1);
		}

		Color GetDefaultBorderColor ()
		{
			Gdk.RGBA c;
			if (StyleContext.LookupColor ("borders", out c))
				return c.ToXwtValue ();
			return new Color (0.5, 0.5, 0.5);
		}

		protected override void OnSnapshot (Gtk.Snapshot snapshot)
		{
			int width = Width, height = Height;
			if (width > 0 && height > 0) {
				var bounds = Graphene.Rect.Alloc ();
				bounds.Init (0, 0, width, height);
				using (var cr = snapshot.AppendCairo (bounds))
					Draw (cr, width, height);
			}
			// children
			base.OnSnapshot (snapshot);
		}

		void Draw (Cairo.Context cr, int width, int height)
		{
			if (backgroundColor.HasValue) {
				cr.Rectangle (0, 0, width, height);
				cr.SetSourceColor (backgroundColor.Value.ToCairoColor ());
				cr.Fill ();
			}

			if (gradientBackground) {
				Color gcol = this.GetBackgroundColor ();
				cr.NewPath ();
				cr.Rectangle (0, 0, width, height);
				using (var pat = new Cairo.LinearGradient (0, 0, 0, height)) {
					pat.AddColorStop (0, gcol.ToCairoColor ());
					gcol.Light -= 0.1;
					pat.AddColorStop (1, gcol.ToCairoColor ());
					cr.SetSource (pat);
					cr.Fill ();
				}
			}

			if (topMargin + bottomMargin + leftMargin + rightMargin == 0)
				return;
			cr.SetSourceColor ((color ?? GetDefaultBorderColor ()).ToCairoColor ());
			cr.Rectangle (0, 0, width, topMargin);
			cr.Rectangle (0, height - bottomMargin, width, bottomMargin);
			cr.Rectangle (0, 0, leftMargin, height);
			cr.Rectangle (width - rightMargin, 0, rightMargin, height);
			cr.Fill ();
		}
	}
}
