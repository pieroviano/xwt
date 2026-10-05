// 
// FontBackendHandler.cs
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

using Xwt.Backends;
using Pango;
using Xwt.Drawing;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Xwt.GtkBackend
{
	public class GtkFontBackendHandler: FontBackendHandler
	{
		static Pango.Context systemContext;

		static GtkFontBackendHandler ()
		{
			systemContext = CreateSystemContext ();
		}

		/// <summary>
		/// A Pango context on the default PangoCairo font map (the one GTK 4 widgets use).
		/// </summary>
		static Pango.Context CreateSystemContext ()
		{
			using (var sf = new Cairo.ImageSurface (Cairo.Format.Argb32, 1, 1))
			using (var cr = new Cairo.Context (sf))
				return Pango.CairoHelper.CreateContext (cr);
		}

		public override object GetSystemDefaultFont ()
		{
			// GTK 4: no Gtk.Rc styles; the default UI font is the gtk-font-name setting
			var name = Gtk.Settings.Default?.GtkFontName;
			return FontDescription.FromString (string.IsNullOrEmpty (name) ? "Sans 10" : name);
		}

		public override IEnumerable<string> GetInstalledFonts ()
		{
			var fontNames = systemContext.FontMap.Families.Select (f => f.Name);
			if (Platform.IsMac) {
				var macFonts = new string [] { "-apple-system-font", ".AppleSystemUIFont" }.AsEnumerable ();
				return macFonts.Concat (fontNames);
			}
			return fontNames;
		}

		public override IEnumerable<KeyValuePair<string, object>> GetAvailableFamilyFaces (string family)
		{
			FontFamily pangoFamily = systemContext.FontMap.Families.FirstOrDefault (f => f.Name == family);
			if (pangoFamily != null) {
				foreach (var face in pangoFamily.Faces)
					yield return new KeyValuePair<string, object>(face.FaceName, face.Describe ());
			}
			yield break;
		}

		public override object Create (string fontName, double size, FontStyle style, FontWeight weight, FontStretch stretch)
		{
			var result = FontDescription.FromString (fontName + " " + size.ToString (CultureInfo.InvariantCulture));
			result.Style = (Pango.Style)style;
			result.Weight = (Pango.Weight)weight;
			result.Stretch = (Pango.Stretch)stretch;
			return result;
		}

		public override bool RegisterFontFromFile (string fontPath)
		{
			var result = AddFontFile (fontPath);
			if (result)
				systemContext.FontMap.Changed ();
			return result;
		}

		protected virtual bool AddFontFile (string fontPath)
		{
			// Adds the font file to the default font map (pango_font_map_add_font_file, Pango >= 1.56),
			// which replaces the fontconfig-specific FcConfigAppFontAddFile registration.
			try {
				return systemContext.FontMap.AddFontFile (fontPath);
			} catch (Exception) {
				// GError, or the symbol is missing in an older Pango
				return false;
			}
		}

		#region IFontBackendHandler implementation
		
		public override object Copy (object handle)
		{
			FontDescription d = (FontDescription) handle;
			return d.Copy ();
		}
		
		public override object SetSize (object handle, double size)
		{
			FontDescription d = (FontDescription) handle;
			d = d.Copy ();
			d.Size = (int) (size * Pango.Scale.PangoScale);
			return d;
		}

		public override object SetFamily (object handle, string family)
		{
			FontDescription fd = (FontDescription) handle;
			fd = fd.Copy ();
			fd.Family = family;
			return fd;
		}

		public override object SetStyle (object handle, FontStyle style)
		{
			FontDescription fd = (FontDescription) handle;
			fd = fd.Copy ();
			fd.Style = (Pango.Style)(int)style;
			return fd;
		}

		public override object SetWeight (object handle, FontWeight weight)
		{
			FontDescription fd = (FontDescription) handle;
			fd = fd.Copy ();
			fd.Weight = (Pango.Weight)(int)weight;
			return fd;
		}

		public override object SetStretch (object handle, FontStretch stretch)
		{
			FontDescription fd = (FontDescription) handle;
			fd = fd.Copy ();
			fd.Stretch = (Pango.Stretch)(int)stretch;
			return fd;
		}
		
		public override double GetSize (object handle)
		{
			FontDescription fd = (FontDescription) handle;
			return (double)fd.Size / (double) Pango.Scale.PangoScale;
		}

		public override string GetFamily (object handle)
		{
			FontDescription fd = (FontDescription) handle;
			return fd.Family;
		}

		public override FontStyle GetStyle (object handle)
		{
			FontDescription fd = (FontDescription) handle;
			return (FontStyle)(int)fd.Style;
		}

		public override FontWeight GetWeight (object handle)
		{
			FontDescription fd = (FontDescription) handle;
			return (FontWeight)(int)fd.Weight;
		}

		public override FontStretch GetStretch (object handle)
		{
			FontDescription fd = (FontDescription) handle;
			return (FontStretch)(int)fd.Stretch;
		}
		#endregion


	}
}

