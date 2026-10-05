//
// CustomCellRendererImage.cs
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
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Xwt.Backends;

namespace Xwt.GtkBackend
{

	public class CustomCellRendererImage: CellViewBackend
	{
		ImageRenderer renderer;

		public CustomCellRendererImage ()
		{
			renderer = new ImageRenderer ();
			renderer.Xalign = renderer.Yalign = 0.5f;
			CellRenderer = renderer;
		}

		protected override void OnLoadData ()
		{
			var view = (IImageCellViewFrontend)Frontend;
			renderer.Context = ApplicationContext;
			renderer.Mode = CellRendererMode.Activatable;
			renderer.Image = view.Image.ToImageDescription (ApplicationContext);
		}
	}

	class ImageRenderer: GtkCellRendererCustom
	{
		ImageDescription image;

		public ApplicationContext Context;

		[GLib.Property ("image")]
		public ImageDescription Image {
			get { return image; }
			set { image = value; }
		}

		protected override void OnSnapshot (Gtk.Snapshot snapshot, Gtk.Widget widget, Gdk.Rectangle background_area, Gdk.Rectangle cell_area, CellRendererState flags)
		{
			if (image.IsNull)
				return;
			var img = image;
			if ((flags & CellRendererState.Selected) != 0) {
				img = new ImageDescription {
					Backend = img.Backend,
					Size = img.Size,
					Alpha = img.Alpha,
					Styles = img.Styles.Add ("sel")
				};
			}
			var pix = ((GtkImage)img.Backend);
			int width = (int) image.Size.Width;
			int height = (int) image.Size.Height;
			int x_offset = 0, y_offset = 0;
			if (!cell_area.IsEmpty && width > 0 && height > 0) {
				x_offset = (int)Math.Max (0, Xalign * (cell_area.Width - width));
				y_offset = (int)Math.Max (0, Yalign * (cell_area.Height - height));
			}
			var bounds = Graphene.Rect.Alloc ();
			bounds.Init (cell_area.X, cell_area.Y, cell_area.Width, cell_area.Height);
			using (var cr = snapshot.AppendCairo (bounds))
				pix.Draw (Context, cr, Util.GetScaleFactor (widget), cell_area.X + x_offset, cell_area.Y + y_offset, img);
		}

		protected override void OnGetPreferredWidth (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			minimum_size = natural_size = image.IsNull ? 0 : (int) image.Size.Width;
		}

		protected override void OnGetPreferredWidthForHeight (Gtk.Widget widget, int height, out int minimum_width, out int natural_width)
		{
			OnGetPreferredWidth (widget, out minimum_width, out natural_width);
		}

		protected override void OnGetPreferredHeight (Gtk.Widget widget, out int minimum_size, out int natural_size)
		{
			minimum_size = natural_size = image.IsNull ? 0 : (int) image.Size.Height;
		}

		protected override void OnGetPreferredHeightForWidth (Gtk.Widget widget, int width, out int minimum_height, out int natural_height)
		{
			OnGetPreferredHeight (widget, out minimum_height, out natural_height);
		}
	}
}
