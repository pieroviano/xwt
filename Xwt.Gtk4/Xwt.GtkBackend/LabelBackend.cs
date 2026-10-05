// 
// LabelBackend.cs
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
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using Xwt.Backends;
using Xwt.Drawing;


namespace Xwt.GtkBackend
{
	public class LabelBackend: WidgetBackend, ILabelBackend
	{
		Color? textColor;
		Alignment alignment;
		FormattedText formattedText;

		public LabelBackend ()
		{
			Widget = Gtk.Label.New (null);
			Label.Xalign = 0;
			Label.Yalign = 0.5f;
			Label.ActivateLink += HandleActivateLink;
		}

		new ILabelEventSink EventSink {
			get { return (ILabelEventSink)base.EventSink; }
		}

		protected Gtk.Label Label {
			get { return (Gtk.Label) Widget; }
		}

		void HandleActivateLink (object o, Gtk.ActivateLinkArgs args)
		{
			// GTK 4: links are Pango markup <a> elements; GtkSharp's ActivateLinkArgs has no Uri member,
			// so the URI comes from the raw signal args (or the label's current link).
			string uri = args.Args != null && args.Args.Length > 0 ? args.Args [0] as string : null;
			if (uri == null)
				uri = Label.CurrentUri;
			args.RetVal = OnActivateLink (uri);
		}

		/// <summary>
		/// Handles the activation of a link; returns true when the link has been handled
		/// (otherwise GTK opens the URI itself).
		/// </summary>
		protected virtual bool OnActivateLink (string uri)
		{
			Uri target = null;
			if (!string.IsNullOrEmpty (uri))
				Uri.TryCreate (uri, UriKind.RelativeOrAbsolute, out target);
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnLinkClicked (target);
			});
			return true;
		}

		public virtual string Text {
			get { return Label.Text; }
			set {
				formattedText = null;
				Label.UseMarkup = false;
				Label.Attributes = null;
				Label.Text = value ?? string.Empty;
			}
		}

		public bool Selectable {
			get { return Label.Selectable; }
			set { Label.Selectable = value; }
		}

		public void SetFormattedText (FormattedText text)
		{
			formattedText = text;
			if (text == null) {
				Text = string.Empty;
				return;
			}
			if (text.Attributes.OfType<LinkTextAttribute> ().Any ()) {
				// links need markup so that GTK handles hover, focus and the activate-link signal
				Label.Attributes = null;
				Label.Markup = FormattedTextMarkup.ToMarkup (text, true);
			} else {
				Label.UseMarkup = false;
				Label.Text = text.Text ?? string.Empty;
				Label.ApplyFormattedText (text);
			}
		}

		public Color TextColor {
			get {
				return textColor.HasValue ? textColor.Value : Label.GetForegroundColor ();
			}
			set {
				textColor = value;
				Label.SetForegroundColor (value);
			}
		}

		public Alignment TextAlignment {
			get {
				return alignment;
			}
			set {
				alignment = value;
				SetAlignment ();
			}
		}

		protected void SetAlignment ()
		{
			switch (alignment) {
				case Alignment.Start:
					Label.Justify = Gtk.Justification.Left;
					Label.Xalign = 0f;
					break;
				case Alignment.End:
					Label.Justify = Gtk.Justification.Right;
					Label.Xalign = 1f;
					break;
				case Alignment.Center:
					Label.Justify = Gtk.Justification.Center;
					Label.Xalign = 0.5f;
					break;
			}
		}

		public EllipsizeMode Ellipsize {
			get {
				return Label.Ellipsize.ToXwtValue ();
			}
			set {
				Label.Ellipsize = value.ToGtkValue ();
			}
		}

		public WrapMode Wrap {
			get {
				if (!Label.Wrap)
					return WrapMode.None;
				switch (Label.WrapMode) {
				case Pango.WrapMode.Char:
					return WrapMode.Character;
				case Pango.WrapMode.Word:
					return WrapMode.Word;
				case Pango.WrapMode.WordChar:
					return WrapMode.WordAndCharacter;
				default:
					return WrapMode.None;
				}
			}
			set {
				Label.Wrap = value != WrapMode.None;
				switch (value) {
				case WrapMode.Character:
					Label.WrapMode = Pango.WrapMode.Char;
					break;
				case WrapMode.Word:
					Label.WrapMode = Pango.WrapMode.Word;
					break;
				case WrapMode.WordAndCharacter:
					Label.WrapMode = Pango.WrapMode.WordChar;
					break;
				}
				SetAlignment ();
			}
		}
	}

	/// <summary>
	/// Converts a <see cref="FormattedText"/> to Pango markup (used where GTK 4 needs markup instead of
	/// an attribute list: label links and menu model labels).
	/// </summary>
	static class FormattedTextMarkup
	{
		public static string ToMarkup (FormattedText text, bool withLinks)
		{
			var s = text.Text ?? string.Empty;
			var bounds = new SortedSet<int> { 0, s.Length };
			foreach (var a in text.Attributes) {
				bounds.Add (Clamp (a.StartIndex, s.Length));
				bounds.Add (Clamp (a.StartIndex + a.Count, s.Length));
			}
			var b = bounds.ToArray ();
			var sb = new StringBuilder ();
			for (int i = 0; i < b.Length - 1; i++) {
				int start = b [i], end = b [i + 1];
				if (end <= start)
					continue;
				var attrs = text.Attributes.Where (a => a.StartIndex <= start && a.StartIndex + a.Count >= end).ToList ();
				var link = withLinks ? attrs.OfType<LinkTextAttribute> ().LastOrDefault () : null;
				var span = GetSpanAttributes (attrs);
				if (link != null)
					sb.Append ("<a href=\"").Append (Escape (link.Target != null ? link.Target.ToString () : string.Empty)).Append ("\">");
				if (span.Length > 0)
					sb.Append ("<span").Append (span).Append ('>');
				sb.Append (Escape (s.Substring (start, end - start)));
				if (span.Length > 0)
					sb.Append ("</span>");
				if (link != null)
					sb.Append ("</a>");
			}
			return sb.ToString ();
		}

		static int Clamp (int v, int max)
		{
			return v < 0 ? 0 : (v > max ? max : v);
		}

		public static string Escape (string s)
		{
			return SecurityElement.Escape (s) ?? string.Empty;
		}

		static string ToHex (Color c)
		{
			return string.Format (CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}", (int)(c.Red * 255), (int)(c.Green * 255), (int)(c.Blue * 255));
		}

		static string GetSpanAttributes (List<TextAttribute> attrs)
		{
			var sb = new StringBuilder ();
			foreach (var a in attrs) {
				if (a is ColorTextAttribute ca)
					sb.Append (" foreground=\"").Append (ToHex (ca.Color)).Append ('"');
				else if (a is BackgroundTextAttribute ba)
					sb.Append (" background=\"").Append (ToHex (ba.Color)).Append ('"');
				else if (a is FontWeightTextAttribute wa)
					sb.Append (" weight=\"").Append (((int)wa.Weight).ToString (CultureInfo.InvariantCulture)).Append ('"');
				else if (a is FontStyleTextAttribute sa)
					sb.Append (" style=\"").Append (sa.Style == FontStyle.Italic ? "italic" : sa.Style == FontStyle.Oblique ? "oblique" : "normal").Append ('"');
				else if (a is UnderlineTextAttribute ua)
					sb.Append (" underline=\"").Append (ua.Underline ? "single" : "none").Append ('"');
				else if (a is StrikethroughTextAttribute st)
					sb.Append (" strikethrough=\"").Append (st.Strikethrough ? "true" : "false").Append ('"');
				else if (a is FontSizeTextAttribute fs)
					sb.Append (" size=\"").Append (((int)(fs.Size * Pango.Scale.PangoScale)).ToString (CultureInfo.InvariantCulture)).Append ('"');
				else if (a is FontTextAttribute fa && fa.Font != null) {
					var fd = Toolkit.GetBackend (fa.Font) as Pango.FontDescription;
					if (fd != null)
						sb.Append (" font_desc=\"").Append (Escape (fd.ToString ())).Append ('"');
				}
			}
			return sb.ToString ();
		}
	}
}
