//
// RichTextViewBackend.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//
// Copyright (c) 2012 Xamarin, Inc.
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

using Xwt;
using Xwt.Backends;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	public class RichTextViewBackend : WidgetBackend, IRichTextViewBackend
	{
		Gtk.TextTagTable table;

		double ParagraphSpacing {
			get {
				var font = Font as Pango.FontDescription;
				var size = font.SizeIsAbsolute ? font.Size : font.Size / Pango.Scale.PangoScale;
				return size / 2; // default to 1/2 of the font/line size
			}
		}

		public RichTextViewBackend ()
		{
			Widget = new GtkTextView ();
			ReadOnly = true;
			Widget.WrapMode = Gtk.WrapMode.Word;
			InitTagTable ();
		}

		void InitTagTable ()
		{
			table = new Gtk.TextTagTable ();
			table.Add (new Gtk.TextTag ("bold") {
				Weight = Pango.Weight.Bold
			});
			table.Add (new Gtk.TextTag ("italic") {
				Style = Pango.Style.Italic
			});
			table.Add (new Gtk.TextTag ("tt") {
				Family = "Monospace"
			});
			table.Add (new Gtk.TextTag ("li") {
				LeftMargin = 14
			});
			table.Add (new Gtk.TextTag ("h1") {
				Weight = Pango.Weight.Bold,
				Scale = Pango.Scale.XXLarge
			});
			table.Add (new Gtk.TextTag ("h2") {
				Weight = Pango.Weight.Bold,
				Scale = Pango.Scale.XLarge
			});
			table.Add (new Gtk.TextTag ("h3") {
				Weight = Pango.Weight.Bold,
				Scale = Pango.Scale.Large
			});
			table.Add (new Gtk.TextTag ("h4") {
				Scale = Pango.Scale.Large
			});
			table.Add (new Gtk.TextTag ("pre") {
				Family = "Monospace",
				Indent = 14,
				WrapMode = Gtk.WrapMode.None
			});
			table.Add (new Gtk.TextTag ("p") {
				SizePoints = Math.Max (LineSpacing, ParagraphSpacing),
			});
			
			table.Add(new Gtk.TextTag ("textColor"));
		}

		private new GtkTextView Widget {
			get {
				return (GtkTextView)base.Widget;
			}
			set {
				base.Widget = value;
			}
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is RichTextViewEvent) {
				switch ((RichTextViewEvent) eventId) {
				case RichTextViewEvent.NavigateToUrl:
					Widget.NavigateToUrl += HandleNavigateToUrl;
					break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is RichTextViewEvent) {
				switch ((RichTextViewEvent) eventId) {
				case RichTextViewEvent.NavigateToUrl:
					Widget.NavigateToUrl -= HandleNavigateToUrl;
					break;
				}
			}
		}

		public IRichTextBuffer CreateBuffer ()
		{
			return new RichTextBuffer (table);
		}

		public void SetBuffer (IRichTextBuffer buffer)
		{
			var buf = buffer as RichTextBuffer;
			if (buf == null)
				throw new ArgumentException ("Passed buffer is of incorrect type", "buffer");

			Widget.Buffer = buf;
		}

		public IRichTextBuffer CurrentBuffer {
			get {
				return Widget.Buffer as RichTextBuffer;
			}
		}

		public bool ReadOnly { 
			get { 
				return !Widget.Editable;
			}
			set {
				Widget.Editable = !value;
				Widget.CursorVisible = !value;
			}
		}

		public bool Selectable {
			get {
				return Widget.Selectable;
			}
			set {
				Widget.Selectable = value;
			}
		}

		public int LineSpacing {
			get {
				return Widget.PixelsInsideWrap;
			}
			set {
				Widget.PixelsInsideWrap = value;
				Widget.PixelsBelowLines = value;
				var tag = table.Lookup ("p");
				tag.SizePoints = Math.Max (value, ParagraphSpacing);
			}
		}

		public Drawing.Color TextColor {
			get {
				var tag = table.Lookup ("textColor");
				return tag.ForegroundRgba.ToXwtValue ();
			}
			set {
				var tag = table.Lookup ("textColor");
				tag.ForegroundRgba = value.ToGtkRgbaValue ();
				var buffer = Widget.Buffer;
				buffer.ApplyTag (tag, buffer.StartIter, buffer.EndIter);
			}
		}

		public override object Font {
			get {
				return base.Font;
			}
			set {
				base.Font = value;
				var tag = table.Lookup ("p");
				tag.SizePoints = Math.Max (LineSpacing, ParagraphSpacing);
			}
		}

		protected override void OnSetBackgroundColor(Drawing.Color color)
		{
			// the text area is a CSS sub-node: the view applies the color to both
			Widget.BackgroundColor = color;
		}

		void HandleNavigateToUrl (object sender, NavigateToUrlEventArgs e)
		{
			ApplicationContext.InvokeUserCode (delegate {
				((IRichTextViewEventSink)EventSink).OnNavigateToUrl (e.Uri);
			});
		}

		class Link {
			public string Title;
			public Uri Href;
			public Gtk.TextMark StartMark;
		}

		class RichTextBuffer : Gtk.TextBuffer, IRichTextBuffer
		{
			const string NewLine = "\n";

			bool needsParagraphBreak;
			bool needsListBreak;

			void BreakParagraph ()
			{
				if (needsParagraphBreak) {
					var iterEnd = EndIter;
					Insert (ref iterEnd, NewLine);

					var m = CreateMark (null, iterEnd, true);
					Insert (ref iterEnd, NewLine);
					var iterStart = GetIterAtMark (m);
					ApplyTag ("p", iterStart, iterEnd);
					DeleteMark (m);

					needsParagraphBreak = false;
					return;
				}
			}

			void BreakList ()
			{
				if (needsListBreak) {
					var end = EndIter;
					Insert (ref end, NewLine);
					needsListBreak = false;
					return;
				}
			}

			public Dictionary<Gtk.TextTag, Link> Links {
				get; private set;
			}

			struct StartState {
				public Gtk.TextMark Mark;
				public int Data;
				public StartState (Gtk.TextMark mark, int data)
				{
					Mark = mark;
					Data = data;
				}
			}
			Stack<StartState> openHeaders;
			Stack<Link> openLinks;

			public RichTextBuffer (Gtk.TextTagTable table) : base (table)
			{
				Links = new Dictionary<Gtk.TextTag, Link> ();
				openHeaders = new Stack<StartState> ();
				openLinks = new Stack<Link> ();
			}

			public string PlainText {
				get {
					return Text;
				}
			}

			public void EmitText (string text, RichTextInlineStyle style)
			{
				var iterEnd = EndIter;
				var m = CreateMark (null, iterEnd, true);
				Insert (ref iterEnd, text);
				var iterStart = GetIterAtMark (m);

				if ((style & RichTextInlineStyle.Bold) != 0)
					ApplyTag ("bold", iterStart, iterEnd);

				if ((style & RichTextInlineStyle.Italic) != 0)
					ApplyTag ("italic", iterStart, iterEnd);

				if ((style & RichTextInlineStyle.Monospace) != 0)
					ApplyTag ("tt", iterStart, iterEnd);

				DeleteMark (m);
			}

			public void EmitStartHeader (int level)
			{
				BreakParagraph ();
				var iter = EndIter;
				openHeaders.Push (new StartState (CreateMark (null, iter, true), level));
			}

			public void EmitEndHeader ()
			{
				var start = openHeaders.Pop ();
				var iterStart = GetIterAtMark (start.Mark);
				var iterEnd = EndIter;
				ApplyTag ("h" + start.Data, iterStart, iterEnd);
				DeleteMark (start.Mark);
				needsParagraphBreak = true;
			}

			public void EmitStartParagraph (int indentLevel)
			{
				BreakParagraph ();
				//FIXME: support indentLevel
			}

			public void EmitEndParagraph ()
			{
				needsParagraphBreak = true;
			}

			public void EmitOpenList ()
			{
				BreakParagraph ();
			}

			public void EmitOpenBullet ()
			{
				BreakList ();
				var iter = EndIter;
				InsertWithTagsByName (ref iter, "• ", "li");
			}

			public void EmitCloseBullet ()
			{
				needsListBreak = true;
			}

			public void EmitCloseList ()
			{
				needsListBreak = false;
				needsParagraphBreak = true;
			}

			public void EmitStartLink (string href, string title)
			{
				var iter = EndIter;

				Uri uri;
				if (!Uri.TryCreate (href, UriKind.RelativeOrAbsolute, out uri))
					uri = null;

				openLinks.Push (new Link ()
				{
					Title = title,
					Href = uri,
					StartMark = CreateMark (null, iter, true),
				});
			}

			public void EmitEndLink ()
			{
				var link = openLinks.Pop ();
				var tag = new Gtk.TextTag (null);
				tag.Underline = Pango.Underline.Single;
				tag.ForegroundRgba = Toolkit.CurrentEngine.Defaults.FallbackLinkColor.ToGtkRgbaValue ();
				TagTable.Add (tag);
				ApplyTag (tag, GetIterAtMark (link.StartMark), EndIter);
				Links[tag] = link;
			}

			public void EmitCodeBlock (string code)
			{
				BreakParagraph ();
				var iter = EndIter;
				InsertWithTagsByName (ref iter, code.Trim (), "pre");
				needsParagraphBreak = true;
			}

			public void EmitHorizontalRuler ()
			{
				//FIXME
			}

			public void EmitText (FormattedText markup)
			{
				var iterEnd = EndIter;
				var textmark = CreateMark (null, iterEnd, true);
				Insert (ref iterEnd, markup.Text);

				foreach (var attr in markup.Attributes) {
					var iterEndAttr = GetIterAtMark (textmark);
					iterEndAttr.ForwardChars (attr.StartIndex);
					var attrStart = CreateMark (null, iterEndAttr, true);
					iterEndAttr.ForwardChars (attr.Count);

					var tag = new Gtk.TextTag (null);

					if (attr is BackgroundTextAttribute) {
						var xa = (BackgroundTextAttribute)attr;
						tag.BackgroundRgba = xa.Color.ToGtkRgbaValue ();
					} else if (attr is ColorTextAttribute) {
						var xa = (ColorTextAttribute)attr;
						tag.ForegroundRgba = xa.Color.ToGtkRgbaValue ();
					} else if (attr is FontWeightTextAttribute) {
						var xa = (FontWeightTextAttribute)attr;
						tag.Weight = (Pango.Weight)(int)xa.Weight;
					} else if (attr is FontStyleTextAttribute) {
						var xa = (FontStyleTextAttribute)attr;
						tag.Style = (Pango.Style)(int)xa.Style;
					} else if (attr is UnderlineTextAttribute) {
						var xa = (UnderlineTextAttribute)attr;
						tag.Underline = xa.Underline ? Pango.Underline.Single : Pango.Underline.None;
					} else if (attr is StrikethroughTextAttribute) {
						var xa = (StrikethroughTextAttribute)attr;
						tag.Strikethrough = xa.Strikethrough;
					} else if (attr is FontTextAttribute) {
						var xa = (FontTextAttribute)attr;
						tag.FontDesc = (Pango.FontDescription)Toolkit.GetBackend (xa.Font);
					} else if (attr is LinkTextAttribute) {
						var xa = (LinkTextAttribute)attr;
						Uri uri = xa.Target;
						if (uri == null)
							Uri.TryCreate (markup.Text.Substring (xa.StartIndex, xa.Count), UriKind.RelativeOrAbsolute, out uri);
						var link = new Link { Href = uri };
						tag.Underline = Pango.Underline.Single;
						tag.ForegroundRgba = Toolkit.CurrentEngine.Defaults.FallbackLinkColor.ToGtkRgbaValue ();
						Links [tag] = link;
					}

					TagTable.Add (tag);
					ApplyTag (tag, GetIterAtMark (attrStart), iterEndAttr);
					DeleteMark (attrStart);
				}
				DeleteMark (textmark);
			}
		}

		class GtkTextView : Gtk.TextView
		{
			static int cssCounter;

			bool selectable = true;
			Link activeLink;
			Link pressedLink;
			bool pressClaimed;
			readonly string cssClass;
			readonly Gtk.CssProvider cssProvider = new Gtk.CssProvider ();
			Drawing.Color? backgroundColor;

			public GtkTextView ()
			{
				cssClass = "xwt-richtext-" + (++cssCounter).ToString (System.Globalization.CultureInfo.InvariantCulture);
				AddCssClass (cssClass);
				StyleContext.AddProvider (cssProvider, Gtk.StyleProviderPriority.Application + 10);

				var motion = new Gtk.EventControllerMotion ();
				motion.Motion += HandleMotion;
				motion.Leave += HandleLeave;
				AddController (motion);

				// capture phase: runs before GtkTextView's own selection gestures
				var click = new Gtk.GestureClick ();
				click.Button = 0;
				click.PropagationPhase = Gtk.PropagationPhase.Capture;
				click.Pressed += HandlePressed;
				click.Released += HandleReleased;
				AddController (click);

				AddNotification ("sensitive", (o, a) => SetDefaultCursor ());
				AddNotification ("buffer", (o, a) => UpdateLinkColor ());
				SetDefaultCursor ();
				UpdateBackground ();
			}

			public bool Selectable {
				get {
					return selectable;
				}
				set {
					selectable = value;
					SetDefaultCursor ();
					UpdateBackground ();
				}
			}

			public Drawing.Color? BackgroundColor {
				get { return backgroundColor; }
				set {
					backgroundColor = value;
					UpdateBackground ();
				}
			}

			public new RichTextBuffer Buffer {
				get {
					return base.Buffer as RichTextBuffer;
				}
				set {
					var buffer = value as RichTextBuffer;
					if (buffer == null)
						throw new InvalidOperationException ();
					base.Buffer = buffer;
					UpdateLinkColor ();
				}
			}

			public event EventHandler<NavigateToUrlEventArgs> NavigateToUrl;

			void HandleMotion (object o, Gtk.MotionArgs args)
			{
				Link link = GetLinkAtPos (args.X, args.Y);
				if (link != null) {
					if (activeLink == null) {
						TooltipText = link.Title;
						Cursor = WidgetBackend.GetCursor (CursorType.Hand);
					} else if (activeLink.Title != link.Title) {
						TooltipText = link.Title;
					}
					activeLink = link;
				} else if (activeLink != null) {
					activeLink = null;
					TooltipText = null;
					SetDefaultCursor ();
				}
			}

			void HandleLeave (object sender, EventArgs e)
			{
				if (activeLink != null) {
					activeLink = null;
					TooltipText = null;
					SetDefaultCursor ();
				}
			}

			void HandlePressed (object o, Gtk.PressedArgs args)
			{
				var gesture = (Gtk.GestureClick)o;
				pressedLink = null;
				pressClaimed = false;
				if (gesture.CurrentButton == (uint)PointerButton.Left)
					pressedLink = GetLinkAtPos (args.X, args.Y);
				// a click on a link navigates, and a non-selectable view must not start a selection:
				// claiming the sequence keeps GtkTextView's own gestures from handling it
				if ((pressedLink != null && NavigateToUrl != null) || !selectable) {
					gesture.SetState (Gtk.EventSequenceState.Claimed);
					pressClaimed = true;
				}
			}

			void HandleReleased (object o, Gtk.ReleasedArgs args)
			{
				var gesture = (Gtk.GestureClick)o;
				if (NavigateToUrl != null && gesture.CurrentButton == (uint)PointerButton.Left) {
					Link link = GetLinkAtPos (args.X, args.Y) ?? (pressClaimed ? pressedLink : null);
					if (link?.Href != null)
						NavigateToUrl?.Invoke (this, new NavigateToUrlEventArgs (link.Href));
				}
				pressedLink = null;
				pressClaimed = false;
			}

			Link GetLinkAtPos (double mousex, double mousey)
			{
				int x, y;
				WindowToBufferCoords (Gtk.TextWindowType.Widget, (int)mousex, (int)mousey, out x, out y);
				Gtk.TextIter iter;
				if (!GetIterAtLocation (out iter, x, y))
					return null;
				if (Buffer != null) {
					foreach (var l in Buffer.Links) {
						if (iter.HasTag (l.Key)) {
							return l.Value;
						}
					}
				}
				return null;
			}

			void SetDefaultCursor ()
			{
				Cursor = Sensitive && Selectable ? WidgetBackend.GetCursor (CursorType.IBeam) : null;
			}

			void UpdateLinkColor ()
			{
				// GTK 4: there is no "link-color" style property; use the theme's named link color when defined
				Gdk.RGBA color;
				if (!StyleContext.LookupColor ("link_color", out color) && !StyleContext.LookupColor ("accent_color", out color))
					color = Toolkit.CurrentEngine.Defaults.FallbackLinkColor.ToGtkRgbaValue ();
				if (Buffer != null)
					foreach (var linkTag in Buffer.Links.Keys)
						linkTag.ForegroundRgba = color;
			}

			protected override void OnRealized ()
			{
				base.OnRealized ();
				UpdateLinkColor ();
			}

			void UpdateBackground ()
			{
				// the text area is the "text" CSS sub-node of the textview: style it through a provider on this view.
				// A non-selectable view uses the parent's background (GTK 2 used the insensitive background).
				string bg = backgroundColor.HasValue ? backgroundColor.Value.ToCssValue () : (Selectable ? null : "transparent");
				var css = bg == null ? "" :
					"." + cssClass + ", ." + cssClass + " > text { background-color: " + bg + "; }";
				cssProvider.LoadFromString (css);
			}
		}
	}
}

