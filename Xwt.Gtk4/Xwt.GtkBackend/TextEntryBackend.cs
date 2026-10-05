// 
// TextEntryBackend.cs
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
using Xwt.Drawing;


namespace Xwt.GtkBackend
{
	public class TextEntryBackend : WidgetBackend, ITextEntryBackend
	{
		public override void Initialize ()
		{
			Widget = new Gtk.Entry ();
		}

		internal protected virtual Gtk.Entry TextEntry {
			get { return (Gtk.Entry)base.Widget; }
		}
		
		protected new Gtk.Entry Widget {
			get { return TextEntry; }
			set { base.Widget = value; }
		}
		
		protected new ITextEntryEventSink EventSink {
			get { return (ITextEntryEventSink)base.EventSink; }
		}

		public string Text {
			get { return Widget.Text; }
			set { Widget.Text = value ?? ""; } // null value causes GTK error
		}

		public string PlaceholderText {
			get { return Widget.PlaceholderText; }
			set { Widget.PlaceholderText = value; }
		}

		public Alignment TextAlignment {
			get {
				if (Widget.Xalign == 0)
					return Alignment.Start;
				else if (Widget.Xalign == 1)
					return Alignment.End;
				else
					return Alignment.Center;
			}
			set {
				switch (value) {
				case Alignment.Start: Widget.Xalign = 0; break;
				case Alignment.End: Widget.Xalign = 1; break;
				case Alignment.Center: Widget.Xalign = 0.5f; break;
				}
			}
		}

		public bool ReadOnly {
			get {
				return !Widget.IsEditable;
			}
			set {
				Widget.IsEditable = !value;
			}
		}
		
		public virtual bool ShowFrame {
			get {
				return Widget.HasFrame;
			}
			set {
				Widget.HasFrame = value;
			}
		}

		public int CursorPosition {
			get {
				return Widget.Position;
			}
			set {
				Widget.Position = value;
			}
		}

		public int SelectionStart {
			get {
				int start, end;
				Widget.GetSelectionBounds (out start, out end);
				return start;
			}
			set {
				int cacheLength = SelectionLength;
				Widget.GrabFocus ();
				if (String.IsNullOrEmpty (Text))
					return;
				// selection changes are reported through notify::cursor-position / notify::selection-bound
				Widget.SelectRegion (value, value + cacheLength);
			}
		}

		public int SelectionLength {
			get {
				int start, end;
				Widget.GetSelectionBounds (out start, out end);
				return end - start;
			}
			set {
				int cacheStart = SelectionStart;
				Widget.GrabFocus ();
				if (String.IsNullOrEmpty (Text))
					return;
				Widget.SelectRegion (cacheStart, cacheStart + value);
			}
		}

		public string SelectedText {
			get {
				int start, end;
				if (!Widget.GetSelectionBounds (out start, out end) || start == end)
					return String.Empty;
				return Widget.GetChars (start, end) ?? String.Empty;
			}
			set {
				int cacheSelStart = SelectionStart;
				int pos = cacheSelStart;
				if (SelectionLength > 0) {
					Widget.DeleteSelection ();
				}
				Widget.InsertText (value ?? String.Empty, ref pos);
				Widget.GrabFocus ();
				Widget.SelectRegion (cacheSelStart, pos);
			}
		}

		public bool MultiLine {
			get; set;
		}

		public bool HasCompletions {
			get {
				return Widget?.Completion?.Model != null;
			}
		}

		/// <summary>
		/// Set the list of completions that will be shown by the entry
		/// </summary>
		/// <param name="completions">The list of completion or null if no completions should be shown</param>
		public void SetCompletions (string[] completions)
		{
			var widgetCompletion = Widget.Completion;

			if (completions == null || completions.Length == 0) {
				if (widgetCompletion != null)
					widgetCompletion.Model = null;
				return;
			}

			if (widgetCompletion == null)
				Widget.Completion = widgetCompletion = CreateCompletion ();

			var model = new Gtk.ListStore (typeof(string));
			foreach (var c in completions)
				model.SetValue (model.Append (), 0, c);
			widgetCompletion.Model = model;
		}

		/// <summary>
		/// Set a custom matching function used to decide which completion from SetCompletions list are shown
		/// for the given input
		/// </summary>
		/// <param name="matchFunc">A function which parameter are, in order, the current text entered by the user and a completion candidate.
		/// Returns true if the candidate should be included in the completion list.</param>
		public void SetCompletionMatchFunc (Func<string, string, bool> matchFunc)
		{
			var widgetCompletion = Widget.Completion;
			if (widgetCompletion == null)
				Widget.Completion = widgetCompletion = CreateCompletion ();
			widgetCompletion.MatchFunc = delegate (Gtk.EntryCompletion completion, string key, Gtk.TreeIter iter) {
				var completionText = (string)completion.Model.GetValue (iter, 0);
				return matchFunc (key, completionText);
			};
		}

		internal static Gtk.EntryCompletion CreateCompletion ()
		{
			// GTK 4: GtkEntryCompletion is deprecated (4.10) but still functional
			return new Gtk.EntryCompletion () {
				PopupCompletion = true,
				TextColumn = 0,
				InlineCompletion = true,
				InlineSelection = true
			};
		}
		
		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is TextEntryEvent) {
				switch ((TextEntryEvent)eventId) {
				case TextEntryEvent.Changed: Widget.Changed += HandleChanged; break;
				case TextEntryEvent.Activated: Widget.Activated += HandleActivated; break;
				case TextEntryEvent.SelectionChanged:
					if (!enableSelectionChangedEvent) {
						enableSelectionChangedEvent = true;
						cacheSelectionStart = SelectionStart;
						cacheSelectionLength = SelectionLength;
						Widget.AddNotification ("cursor-position", HandleSelectionNotify);
						Widget.AddNotification ("selection-bound", HandleSelectionNotify);
					}
					break;
				}
			}
		}
		
		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is TextEntryEvent) {
				switch ((TextEntryEvent)eventId) {
				case TextEntryEvent.Changed: Widget.Changed -= HandleChanged; break;
				case TextEntryEvent.Activated: Widget.Activated -= HandleActivated; break;
				case TextEntryEvent.SelectionChanged:
					if (enableSelectionChangedEvent) {
						enableSelectionChangedEvent = false;
						Widget.RemoveNotification ("cursor-position", HandleSelectionNotify);
						Widget.RemoveNotification ("selection-bound", HandleSelectionNotify);
					}
					break;
				}
			}
		}

		void HandleChanged (object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnChanged);
		}

		void HandleActivated (object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnActivated);
		}

		bool enableSelectionChangedEvent;
		int cacheSelectionStart, cacheSelectionLength;

		void HandleSelectionNotify (object o, GLib.NotifyArgs args)
		{
			// cursor-position and selection-bound are notified separately for a single change: report only real changes
			int start = SelectionStart, length = SelectionLength;
			if (start == cacheSelectionStart && length == cacheSelectionLength)
				return;
			cacheSelectionStart = start;
			cacheSelectionLength = length;
			ApplicationContext.InvokeUserCode (EventSink.OnSelectionChanged);
		}
	}
}
