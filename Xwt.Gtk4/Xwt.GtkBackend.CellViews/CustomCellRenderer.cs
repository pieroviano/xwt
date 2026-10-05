//
// CustomCellRenderer.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc.
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
using Gtk;
using Xwt.CairoBackend;
using Xwt.Backends;
using Xwt.GtkBackend;

namespace Xwt.GtkBackend
{
	class CustomCellRenderer: CellViewBackend, ICanvasCellViewBackend
	{
		Rectangle cellArea;
		Rectangle backgroundArea;
		bool isSelected;
		bool hasFocus;
		bool isPrelit;
		TreeIter lastIter;
		bool shown;

		public override void Initialize (ICellViewFrontend cellView, ICellRendererTarget rendererTarget, object target)
		{
			base.Initialize (cellView, rendererTarget, target);
			CellRenderer = new CanvasRenderer {
				Parent = this,
				CellView = (ICanvasCellViewFrontend) cellView 
			};
		}

		internal void StartDrawing (Rectangle ba, Rectangle ca, Gtk.CellRendererState flags)
		{
			this.cellArea = ca;
			this.backgroundArea = ba;
			isSelected = (flags & CellRendererState.Selected) != 0;
			hasFocus = (flags & CellRendererState.Focused) != 0;
			isPrelit = (flags & CellRendererState.Prelit) != 0;

			// Gtk will rerender the cell on every status change, hence we can expect the values
			// set here to be valid until the geometry or any other status of the cell and tree changes.
			// Setting shown=true ensures that those values are always reused instead if queried
			// from the parent tree after the cell has been rendered.
			shown = true;
		}

		protected override void OnLoadData ()
		{
			if (!CurrentIter.Equals (lastIter)) {
				// if the current iter has changed all cached values from the last draw are invalid
				shown = false;
				lastIter = CurrentIter;
			}
			base.OnLoadData ();
		}

		internal void EndDrawing ()
		{
		}

		public override Rectangle CellBounds {
			get {
				if (shown)
					return cellArea;
				return base.CellBounds;
			}
		}

		public override Rectangle BackgroundBounds {
			get {
				if (shown)
					return backgroundArea;
				return base.BackgroundBounds;
			}
		}

		public override bool Selected {
			get {
				if (shown)
					return isSelected;
				return base.Selected;
			}
		}

		public override bool HasFocus {
			get {
				if (shown)
					return hasFocus;
				return base.HasFocus;
			}
		}

		public bool IsHighlighted {
			get {
				if (shown)
					return isPrelit;
				return false;
			}
		}
	}

	class CanvasRenderer: GtkCellRendererCustom
	{
		public ICanvasCellViewFrontend CellView;
		public CustomCellRenderer Parent;

		protected override void OnSnapshot (Gtk.Snapshot snapshot, Gtk.Widget widget, Gdk.Rectangle background_area, Gdk.Rectangle cell_area, CellRendererState flags)
		{
			// The tree view snapshots cells in bin window coordinates; Xwt cell bounds are
			// widget coordinates, so the areas are shifted and the cairo context compensates.
			int dx = 0, dy = 0;
			var bounds = Graphene.Rect.Alloc ();
			bounds.Init (background_area.X, background_area.Y, background_area.Width, background_area.Height);
			var tree = widget as Gtk.TreeView;
			if (tree != null) {
				int wx, wy;
				tree.ConvertBinWindowToWidgetCoords (cell_area.X, cell_area.Y, out wx, out wy);
				dx = wx - cell_area.X;
				dy = wy - cell_area.Y;
				cell_area.X += dx;
				background_area.X += dx;
				cell_area.Y += dy;
				background_area.Y += dy;
			}

			Parent.StartDrawing (new Rectangle (background_area.X, background_area.Y, background_area.Width, background_area.Height), new Rectangle (cell_area.X, cell_area.Y, cell_area.Width, cell_area.Height), flags);
			var cr = snapshot.AppendCairo (bounds);
			CellView.ApplicationContext.InvokeUserCode (delegate {
				CairoContextBackend ctx = new CairoContextBackend (Util.GetScaleFactor (widget));
				ctx.Context = cr;
				ctx.Context.Translate (-dx, -dy);
				using (ctx) {
					CellView.Draw (ctx, new Rectangle (cell_area.X, cell_area.Y, cell_area.Width, cell_area.Height));
				}
			});
			cr.Dispose ();
			Parent.EndDrawing ();
		}

		Size GetRequiredSize (int width)
		{
			Size size = new Size ();
			var widthConstraint = width > 0 ? SizeConstraint.WithSize (width) : SizeConstraint.Unconstrained;
			CellView.ApplicationContext.InvokeUserCode (delegate {
				size = CellView.GetRequiredSize (widthConstraint);
			});
			return size;
		}

		protected override void OnGetPreferredWidth (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			minimum_size = natural_size = (int) GetRequiredSize (-1).Width;
		}

		protected override void OnGetPreferredWidthForHeight (Gtk.Widget widget, int height, out int minimum_width, out int natural_width)
		{
			OnGetPreferredWidth (widget, out minimum_width, out natural_width);
		}

		protected override void OnGetPreferredHeight (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			minimum_size = natural_size = (int) GetRequiredSize (-1).Height;
		}

		protected override void OnGetPreferredHeightForWidth (Gtk.Widget widget, int width, out int minimum_height, out int natural_height)
		{
			minimum_height = natural_height = (int) GetRequiredSize (width).Height;
		}
	}
}
