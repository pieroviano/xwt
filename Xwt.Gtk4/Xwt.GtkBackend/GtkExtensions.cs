//
// GtkExtensions.cs
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
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public static class GtkExtensions
	{
		#region Per-widget CSS

		class WidgetCss
		{
			public string ClassName;
			public Gtk.CssProvider Provider;
			public SortedDictionary<string, string> Properties = new SortedDictionary<string, string> ();
		}

		static readonly ConditionalWeakTable<Gtk.Widget, WidgetCss> widgetStyles = new ConditionalWeakTable<Gtk.Widget, WidgetCss> ();
		static int cssClassCounter;

		/// <summary>
		/// Sets (or, with a null value, removes) a CSS property on this widget only. GTK 4 replaced
		/// ModifyBg/ModifyFg/ModifyFont and OverrideColor with CSS; each styled widget gets a unique
		/// class and its own provider.
		/// </summary>
		public static void SetStyle (this Gtk.Widget widget, string property, string value)
		{
			WidgetCss css;
			if (!widgetStyles.TryGetValue (widget, out css)) {
				if (value == null)
					return;
				css = new WidgetCss ();
				css.ClassName = "xwt-" + (++cssClassCounter).ToString (CultureInfo.InvariantCulture);
				css.Provider = new Gtk.CssProvider ();
				widget.AddCssClass (css.ClassName);
				widget.StyleContext.AddProvider (css.Provider, Gtk.StyleProviderPriority.Application + 10);
				widgetStyles.Add (widget, css);
			}
			if (value == null)
				css.Properties.Remove (property);
			else
				css.Properties [property] = value;

			var sb = new StringBuilder ();
			sb.Append ('.').Append (css.ClassName).Append (" {");
			foreach (var p in css.Properties)
				sb.Append (' ').Append (p.Key).Append (": ").Append (p.Value).Append (';');
			sb.Append (" }");
			css.Provider.LoadFromString (sb.ToString ());
		}

		public static string GetStyle (this Gtk.Widget widget, string property)
		{
			WidgetCss css;
			string value;
			if (widgetStyles.TryGetValue (widget, out css) && css.Properties.TryGetValue (property, out value))
				return value;
			return null;
		}

		/// <summary>CSS shorthand <c>font</c> value for a Pango font description.</summary>
		public static string ToCssValue (this Pango.FontDescription font)
		{
			var sb = new StringBuilder ();
			var mask = font.SetFields;
			if ((mask & Pango.FontMask.Style) != 0) {
				if (font.Style == Pango.Style.Italic)
					sb.Append ("italic ");
				else if (font.Style == Pango.Style.Oblique)
					sb.Append ("oblique ");
			}
			if ((mask & Pango.FontMask.Weight) != 0)
				sb.Append (((int)font.Weight).ToString (CultureInfo.InvariantCulture)).Append (' ');
			if ((mask & Pango.FontMask.Size) != 0 && font.Size > 0) {
				double size = font.Size / (double)Pango.Scale.PangoScale;
				sb.Append (size.ToString ("0.##", CultureInfo.InvariantCulture)).Append (font.SizeIsAbsolute ? "px " : "pt ");
			} else
				sb.Append ("medium ");
			var family = (mask & Pango.FontMask.Family) != 0 ? font.Family : null;
			if (string.IsNullOrEmpty (family))
				sb.Append ("sans-serif");
			else {
				var families = family.Split (',');
				for (int i = 0; i < families.Length; i++) {
					if (i > 0)
						sb.Append (", ");
					sb.Append ('"').Append (families [i].Trim ().Replace ("\"", "")).Append ('"');
				}
			}
			return sb.ToString ();
		}

		#endregion

		#region Colors

		public static Xwt.Drawing.Color GetBackgroundColor (this Gtk.Widget widget)
		{
			Gdk.RGBA color;
			if (widget.StyleContext.LookupColor ("theme_bg_color", out color) || widget.StyleContext.LookupColor ("window_bg_color", out color))
				return color.ToXwtValue ();
			return Xwt.Drawing.Colors.White;
		}

		public static void SetBackgroundColor (this Gtk.Widget widget, Xwt.Drawing.Color color)
		{
			widget.SetStyle ("background-image", "none");
			widget.SetStyle ("background-color", color.ToCssValue ());
		}

		public static void ResetBackgroundColor (this Gtk.Widget widget)
		{
			widget.SetStyle ("background-image", null);
			widget.SetStyle ("background-color", null);
		}

		public static Xwt.Drawing.Color GetForegroundColor (this Gtk.Widget widget)
		{
			return widget.StyleColor.ToXwtValue ();
		}

		public static void SetForegroundColor (this Gtk.Widget widget, Xwt.Drawing.Color color)
		{
			widget.SetStyle ("color", color.ToCssValue ());
		}

		#endregion

		#region Children

		/// <summary>Direct children of a widget (GTK 4 widgets keep children as a sibling list).</summary>
		public static IEnumerable<Gtk.Widget> GetChildren (this Gtk.Widget widget)
		{
			var c = widget.FirstChild;
			while (c != null) {
				var next = c.NextSibling;
				yield return c;
				c = next;
			}
		}

		#endregion

		public static Gtk.CellRenderer[] GetCellRenderers (this Gtk.TreeViewColumn column)
		{
			return ((Gtk.ICellLayout)column).Cells;
		}

		public static void GetSize (this Gtk.CellRenderer cr, Gtk.Widget widget, ref Gdk.Rectangle cell_area, out int x_offset, out int y_offset, out int width, out int height)
		{
			cr.GetPadding (out x_offset, out y_offset);
			int minW, natW, minH, natH;
			cr.GetPreferredHeightForWidth (widget, cell_area.Width, out minH, out natH);
			cr.GetPreferredWidthForHeight (widget, cell_area.Height, out minW, out natW);
			width = Math.Max (minW, natW);
			height = Math.Max (minH, natH);
			x_offset += (int)(cr.Xalign * (cell_area.Width - width));
			y_offset += (int)(cr.Yalign * (cell_area.Height - height));
		}

		public static double GetSliderPosition (this Gtk.Range range)
		{
			int start, end;
			range.GetSliderRange (out start, out end);
			return start + ((end - start) / 2);
		}

		/// <summary>Converts a Cairo ARGB32 image surface to a pixbuf (gdk_pixbuf_get_from_surface is gone in GTK 4).</summary>
		public static Gdk.Pixbuf ToPixbuf (this Cairo.ImageSurface surface)
		{
			return ImageBuilderBackend.CreatePixbuf (surface);
		}

		/// <summary>Converts a Cairo ARGB32 image surface to a texture.</summary>
		public static Gdk.Texture ToTexture (this Cairo.ImageSurface surface)
		{
			surface.Flush ();
			int width = surface.Width, height = surface.Height, stride = surface.Stride;
			var data = new byte [stride * height];
			System.Runtime.InteropServices.Marshal.Copy (surface.DataPtr, data, 0, data.Length);
			var format = BitConverter.IsLittleEndian ? Gdk.MemoryFormat.B8g8r8a8Premultiplied : Gdk.MemoryFormat.A8r8g8b8Premultiplied;
			using (var bytes = new GLib.Bytes (data))
				return new Gdk.MemoryTexture (width, height, format, bytes, (ulong)stride);
		}
	}
}
