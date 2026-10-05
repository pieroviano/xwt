// 
// SegmentedButtonBackend.cs
//  
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
// 
// Copyright (c) 2013 Xamarin Inc
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
using System.Linq;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// A row of joined buttons: a homogeneous Gtk.Box with the "linked" style class.
	/// </summary>
	public class SegmentedButtonBackend : WidgetBackend, ISegmentedButtonBackend
	{
		public SegmentedButtonBackend ()
		{
		}

		public override void Initialize ()
		{
			var box = new Gtk.Box (Gtk.Orientation.Horizontal, 0);
			box.Homogeneous = true;
			box.AddCssClass ("linked");
			Widget = box;
		}

		protected new Gtk.Box Widget {
			get { return (Gtk.Box)base.Widget; }
			set { base.Widget = value; }
		}

		static Gtk.Widget GetButtonWidget (Button button)
		{
			return ((WidgetBackend)Toolkit.GetBackend (button)).RootWidget;
		}

		Gtk.Widget GetChildAt (int index)
		{
			return index >= 0 ? Widget.GetChildren ().ElementAtOrDefault (index) : null;
		}

		public void AddChildButton (int index, Button button)
		{
			var w = GetButtonWidget (button);
			var sibling = GetChildAt (index - 1);
			if (index <= 0)
				Widget.Prepend (w);
			else if (sibling != null)
				Widget.InsertChildAfter (w, sibling);
			else
				Widget.Append (w);
		}

		public void RemoveChildButton (int index)
		{
			var w = GetChildAt (index);
			if (w != null)
				Widget.Remove (w);
		}

		public void ReplaceChildButton (int index, Button newButton)
		{
			var old = GetChildAt (index);
			var w = GetButtonWidget (newButton);
			if (old == null) {
				Widget.Append (w);
				return;
			}
			Widget.InsertChildAfter (w, old);
			Widget.Remove (old);
		}

		public int Spacing {
			get {
				return Widget.Spacing;
			}
			set {
				Widget.Spacing = value;
			}
		}
	}
}
