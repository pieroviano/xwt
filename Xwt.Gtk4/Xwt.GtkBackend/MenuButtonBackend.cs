// 
// MenuButtonBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//       Michael Hutchinson <mhutch@xamarin.com>
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
	/// <summary>
	/// A Gtk.MenuButton whose menu model is requested from the frontend every time it pops up.
	/// </summary>
	public class MenuButtonBackend: ButtonBackend, IMenuButtonBackend
	{
		bool clickedEnabled;

		public MenuButtonBackend ()
		{
		}

		public override void Initialize ()
		{
			var button = new Gtk.MenuButton ();
			button.CreatePopupFunc = HandleCreatePopup;
			((WidgetBackend)this).Widget = button;
			InitializeButton ();
		}

		protected Gtk.MenuButton MenuButton {
			get { return (Gtk.MenuButton) ((WidgetBackend)this).Widget; }
		}

		new IMenuButtonEventSink EventSink {
			get { return (IMenuButtonEventSink)base.EventSink; }
		}

		protected override Gtk.Widget ButtonContent {
			get { return MenuButton.Child; }
			set { MenuButton.Child = value; }
		}

		protected override void SetButtonLabel (string label)
		{
			MenuButton.Label = label;
		}

		protected override bool ButtonUseUnderline {
			get { return MenuButton.UseUnderline; }
			set { MenuButton.UseUnderline = value; }
		}

		protected override bool ButtonHasFrame {
			get { return MenuButton.HasFrame; }
			set { MenuButton.HasFrame = value; }
		}

		protected override Gtk.Widget FrameWidget {
			// the frame is drawn by the toggle button inside the menu button
			get { return MenuButton.FirstChild ?? MenuButton; }
		}

		protected override void ConnectClicked (bool connect)
		{
			// GTK 4: Gtk.MenuButton has no "clicked" signal; the click is reported when the menu pops up
			clickedEnabled = connect;
		}

		void HandleCreatePopup (Gtk.MenuButton button)
		{
			MenuBackend m = null;
			ApplicationContext.InvokeUserCode (delegate {
				m = (MenuBackend) EventSink.OnCreateMenu ();
			});
			if (m != null) {
				m.InsertActions (button);
				button.MenuModel = m.MenuModel;
			} else
				button.MenuModel = null;

			if (clickedEnabled)
				HandleWidgetClicked (button, EventArgs.Empty);
		}
	}
}
