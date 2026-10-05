// 
// WindowBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//       Konrad Kruczyński <kkruczynski@antmicro.com>
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
	public class WindowBackend: WindowFrameBackend, IWindowBackend, IConstraintProvider
	{
		RootWindowAlignment alignment;
		Gtk.PopoverMenuBar mainMenu;
		Gtk.Box mainBox;

		public override void Initialize ()
		{
			Window = new Gtk.Window ();
			Window.Child = CreateMainLayout ();
		}

		/// <summary>
		/// Creates the window content: a vertical box holding the optional menu bar and the
		/// alignment that hosts the Xwt content.
		/// </summary>
		protected virtual Gtk.Widget CreateMainLayout ()
		{
			mainBox = new Gtk.Box (Gtk.Orientation.Vertical, 0);
			alignment = new RootWindowAlignment (this);
			alignment.Hexpand = alignment.Vexpand = true;
			mainBox.Append (alignment);
			return mainBox;
		}

		public Gtk.Box MainBox {
			get { return mainBox; }
		}

		public override void GetMetrics (out Size minSize, out Size decorationSize)
		{
			if (mainMenu != null) {
				int minW, natW, minH, natH, b1, b2;
				mainMenu.Measure (Gtk.Orientation.Horizontal, -1, out minW, out natW, out b1, out b2);
				mainMenu.Measure (Gtk.Orientation.Vertical, -1, out minH, out natH, out b1, out b2);
				minSize = new Size (minW, 0);
				decorationSize = new Size (0, natH);
			}
			else {
				minSize = decorationSize = Size.Zero;
			}
		}

		public void SetChild (IWidgetBackend child)
		{
			if (alignment.Child != null)
				WidgetBackend.RemoveChildPlacement (alignment.Child);
			alignment.Child = child != null ? WidgetBackend.GetWidgetWithPlacement (child) : null;
		}

		public virtual void UpdateChildPlacement (IWidgetBackend childBackend)
		{
			WidgetBackend.SetChildPlacement (childBackend);
		}

		public void SetMainMenu (IMenuBackend menu)
		{
			if (mainMenu != null) {
				mainBox.Remove (mainMenu);
				mainMenu = null;
			}

			if (menu != null) {
				var m = (MenuBackend) menu;
				mainMenu = m.CreateMenuBar ();
				m.InsertActions (Window);
				mainBox.Prepend (mainMenu);
			}
		}

		public void SetPadding (double left, double top, double right, double bottom)
		{
			alignment.MarginStart = (int) left;
			alignment.MarginEnd = (int) right;
			alignment.MarginTop = (int) top;
			alignment.MarginBottom = (int) bottom;
		}

		public void SetMinSize (Size s)
		{
			// Not required
		}

		public override void SetSize (double width, double height)
		{
			base.SetSize (width, height);
			if (alignment.Child != null)
				alignment.Child.QueueResize ();
		}

		public void GetConstraints (Gtk.Widget target, out SizeConstraint width, out SizeConstraint height)
		{
			width = RequestedSize.Width;
			height = RequestedSize.Height;
		}

		Color? backgroundColor;

		Color IWindowBackend.BackgroundColor {
			get {
				return backgroundColor ?? Window.GetBackgroundColor ();
			}
			set {
				backgroundColor = value;
				Window.SetBackgroundColor (value);
			}
		}
	}

	/// <summary>
	/// Single-child container hosting the window content; provides the window size as constraint
	/// to the content. Its margins are the window padding.
	/// </summary>
	class RootWindowAlignment: ContainerWidget, IConstraintProvider
	{
		WindowBackend backend;
		Gtk.Widget child;

		public RootWindowAlignment (WindowBackend backend)
		{
			this.backend = backend;
			LayoutManager = new Gtk.BinLayout ();
		}

		public Gtk.Widget Child {
			get { return child; }
			set {
				if (child == value)
					return;
				if (child != null)
					RemoveChild (child);
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

		public void GetConstraints (Gtk.Widget target, out SizeConstraint width, out SizeConstraint height)
		{
			backend.GetConstraints (this, out width, out height);
		}
	}
}
