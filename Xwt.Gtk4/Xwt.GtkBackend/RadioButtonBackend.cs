//
// RadioButtonBackend.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
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
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// GTK 4 has no radio button class: a Gtk.CheckButton joined to a group (gtk_check_button_set_group)
	/// is drawn and behaves as a radio button.
	/// </summary>
	public class RadioButtonBackend: WidgetBackend, IRadioButtonBackend
	{
		RadioGroup radioGroup;
		bool internalActiveUpdate;
		bool toggledEnabled;
		bool clickedEnabled;

		public RadioButtonBackend ()
		{
		}

		public override void Initialize ()
		{
			Widget = new Gtk.CheckButton ();
			Widget.Toggled += HandleToggled;
			// a check button is drawn as a radio only when it belongs to a group
			Group = new RadioGroup () { Leader = Widget };
		}

		protected new Gtk.CheckButton Widget {
			get { return (Gtk.CheckButton)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IRadioButtonEventSink EventSink {
			get { return (IRadioButtonEventSink)base.EventSink; }
		}

		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is RadioButtonEvent) {
				switch ((RadioButtonEvent)eventId) {
				case RadioButtonEvent.ActiveChanged: toggledEnabled = true; break;
				case RadioButtonEvent.Clicked: clickedEnabled = true; break;
				}
			}
		}

		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is RadioButtonEvent) {
				switch ((RadioButtonEvent)eventId) {
				case RadioButtonEvent.ActiveChanged: toggledEnabled = false; break;
				case RadioButtonEvent.Clicked: clickedEnabled = false; break;
				}
			}
		}

		void HandleToggled (object sender, EventArgs e)
		{
			if (toggledEnabled)
				ApplicationContext.InvokeUserCode (EventSink.OnToggled);
			// GTK 4: Gtk.CheckButton has no "clicked" signal; a non-programmatic activation is a click
			if (clickedEnabled && !internalActiveUpdate && Widget.Active)
				ApplicationContext.InvokeUserCode (EventSink.OnClicked);
		}

		#region IRadioButtonBackend implementation

		public void SetContent (IWidgetBackend widget)
		{
			RemoveChildPlacement (Widget.Child);
			Widget.Child = widget != null ? GetWidgetWithPlacement (widget) : null;
		}

		public void SetContent (string label)
		{
			RemoveChildPlacement (Widget.Child);
			Widget.Label = label;
		}

		class RadioGroup
		{
			public Gtk.CheckButton Leader;
		}

		public object Group {
			get {
				return radioGroup;
			}
			set {
				var g = (RadioGroup)value;
				if (g != radioGroup) {
					radioGroup = g;
					Widget.Group = g.Leader != Widget ? g.Leader : null;
				}
			}
		}

		public bool Active {
			get {
				return Widget.Active;
			}
			set {
				// GTK 4 allows a grouped check button to be deactivated, leaving no active item
				internalActiveUpdate = true;
				Widget.Active = value;
				internalActiveUpdate = false;
			}
		}

		#endregion
	}
}
