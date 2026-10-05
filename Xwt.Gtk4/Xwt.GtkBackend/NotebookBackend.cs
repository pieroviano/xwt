// 
// NotebookBackend.cs
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
	public class NotebookBackend: WidgetBackend, INotebookBackend
	{
		public NotebookBackend ()
		{
			Widget = new Gtk.Notebook ();
			Widget.Scrollable = true;
		}

		new Gtk.Notebook Widget {
			get { return (Gtk.Notebook)base.Widget; }
			set { base.Widget = value; }
		}

		public override void EnableEvent (object eventId)
		{
			if (eventId is NotebookEvent) {
				NotebookEvent ev = (NotebookEvent) eventId;
				if (ev == NotebookEvent.CurrentTabChanged) {
					Widget.AddNotification ("page", HandleCurrentPageChanged);
				}
			}
			base.EnableEvent (eventId);
		}

		public override void DisableEvent (object eventId)
		{
			if (eventId is NotebookEvent) {
				NotebookEvent ev = (NotebookEvent) eventId;
				if (ev == NotebookEvent.CurrentTabChanged) {
					Widget.RemoveNotification ("page", HandleCurrentPageChanged);
				}
			}
			base.DisableEvent (eventId);
		}

		void HandleCurrentPageChanged (object o, GLib.NotifyArgs args)
		{
			// "page" is notified after the switch (switch-page runs before CurrentPage changes)
			ApplicationContext.InvokeUserCode (((INotebookEventSink)EventSink).OnCurrentTabChanged);
		}

		/// <summary>The page widget of a child: the child itself or its placement wrapper.</summary>
		static Gtk.Widget GetPageWidget (IWidgetBackend widget)
		{
			var w = GetWidget (widget);
			if (w != null && w.Parent is WidgetPlacementWrapper)
				return w.Parent;
			return w;
		}

		public void Add (IWidgetBackend widget, NotebookTab tab)
		{
			Widget.AppendPage (GetWidgetWithPlacement (widget), CreateLabel (tab));
		}

		public void Remove (IWidgetBackend widget)
		{
			var page = GetPageWidget (widget);
			int n = page != null ? Widget.PageNum (page) : -1;
			if (n >= 0)
				Widget.RemovePage (n);
			RemoveChildPlacement (page);
		}

		public void UpdateLabel (NotebookTab tab, string hint)
		{
			IWidgetBackend widget = (IWidgetBackend) Toolkit.GetBackend (tab.Child);
			var page = GetPageWidget (widget);
			if (page != null && Widget.PageNum (page) >= 0)
				Widget.SetTabLabel (page, CreateLabel (tab));
		}

		public int CurrentTab {
			get {
				return Widget.CurrentPage;
			}
			set {
				Widget.CurrentPage = value;
			}
		}

		public Xwt.NotebookTabOrientation TabOrientation {
			get {
				Xwt.NotebookTabOrientation tabPos = Xwt.NotebookTabOrientation.Top;
				Enum.TryParse (Widget.TabPos.ToString (), out tabPos);
				return tabPos;
			}
			set {
				Gtk.PositionType tabPos = Gtk.PositionType.Top;
				Enum.TryParse (value.ToString (), out tabPos);
				Widget.TabPos = tabPos;
			}
		}

		Gtk.Widget CreateLabel (NotebookTab tab)
		{
			return Gtk.Label.New (tab.Label);
		}
	}
}
