// 
// RadioButtonMenuItemBackend.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc
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
	/// Radio item: a string stateful action whose target is the action name (rendered as a radio by GTK).
	/// The backend does not know the radio group (the frontend unchecks the other items), so each item
	/// has its own action: checked when the state equals the target.
	/// </summary>
	public class RadioButtonMenuItemBackend: MenuItemBackend, IRadioButtonMenuItemBackend
	{
		protected override GLib.SimpleAction CreateAction (string name)
		{
			return new GLib.SimpleAction (name, GLib.VariantType.String, new GLib.Variant (string.Empty));
		}

		protected override GLib.Variant ActionTarget {
			get { return new GLib.Variant (Action.Name); }
		}

		public override bool Checked {
			get { return (string) Action.State == Action.Name; }
			set { Action.State = new GLib.Variant (value ? Action.Name : string.Empty); }
		}

		protected override void OnActivated (GLib.Variant parameter)
		{
			Checked = true;
		}
	}
}
