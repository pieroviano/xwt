//
// AlertDialog.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Linq;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// A Gnome HIG compliant alert dialog. GTK 4: GtkMessageDialog is deprecated and
	/// GtkAlertDialog can't show custom icons, option check boxes or button images, so this is
	/// a modal window laid out like a message dialog.
	/// </summary>
	internal class GtkAlertDialog : Gtk.Window
	{
		ApplicationContext actx;
		Command resultButton = null;
		Command[] commands;
		Gtk.Button[] buttons;

		public Command ResultButton {
			get {
				return resultButton;
			}
		}

		public bool ApplyToAll { get; set; }

		public GtkAlertDialog (ApplicationContext actx, MessageDescription message)
		{
			this.actx = actx;
			this.Title = "";
			this.Resizable = false;
			this.Modal = true;
			// closing the window hides it, which ends the modal loop with no result
			this.HideOnClose = true;
			this.commands = message.Buttons.ToArray ();
			AddCssClass ("message");

			string primaryText = String.Empty;
			string secondaryText = String.Empty;

			if (string.IsNullOrEmpty (message.Text)) {
				if (!string.IsNullOrEmpty (message.SecondaryText))
					primaryText = message.SecondaryText;
			} else {
				primaryText = message.Text;
				secondaryText = message.SecondaryText;
			}

			var mainBox = new Gtk.Box (Gtk.Orientation.Vertical, 12);
			mainBox.MarginStart = mainBox.MarginEnd = mainBox.MarginTop = mainBox.MarginBottom = 12;

			var messageBox = new Gtk.Box (Gtk.Orientation.Horizontal, 12);
			if (message.Icon != null) {
				var icon = message.Icon.ToImageDescription (actx).WithDefaultSize (Xwt.IconSize.Large);
				var image = new ImageBox (actx, icon);
				image.Valign = Gtk.Align.Start;
				messageBox.Append (image);
			}

			var textBox = new Gtk.Box (Gtk.Orientation.Vertical, 6);
			textBox.Hexpand = true;
			var primary = new Gtk.Label (null);
			primary.Markup = ("<span weight=\"bold\" size=\"larger\">" + GLib.Markup.EscapeText (primaryText) + "</span>");
			ConfigureMessageLabel (primary);
			textBox.Append (primary);
			if (!String.IsNullOrEmpty (secondaryText)) {
				var secondary = new Gtk.Label (secondaryText);
				ConfigureMessageLabel (secondary);
				textBox.Append (secondary);
			}
			messageBox.Append (textBox);
			mainBox.Append (messageBox);

			foreach (var op in message.Options) {
				var option = op;
				Gtk.CheckButton check = new Gtk.CheckButton (option.Text);
				check.Active = option.Value;
				mainBox.Append (check);
				check.Toggled += delegate {
					message.SetOptionValue (option.Id, check.Active);
				};
			}

			if (message.AllowApplyToAll) {
				Gtk.CheckButton check = new Gtk.CheckButton (Xwt.Application.TranslationCatalog.GetString("Apply to all"));
				mainBox.Append (check);
				check.Toggled += delegate {
					ApplyToAll = check.Active;
				};
			}

			var buttonBox = new Gtk.Box (Gtk.Orientation.Horizontal, 6);
			buttonBox.Halign = Gtk.Align.End;
			buttonBox.MarginTop = 6;
			buttons = new Gtk.Button [commands.Length];
			for (int n = 0; n < commands.Length; n++) {
				var command = commands [n];
				var newButton = new Gtk.Button ();
				if (command.Icon != null) {
					var icon = command.Icon.ToImageDescription (actx).WithDefaultSize (Xwt.IconSize.Small);
					var box = new Gtk.Box (Gtk.Orientation.Horizontal, 3);
					box.Append (new ImageBox (actx, icon));
					var label = new Gtk.Label (null);
					label.TextWithMnemonic = command.Label;
					label.MnemonicWidget = newButton;
					box.Append (label);
					newButton.Child = box;
				} else {
					newButton.Label = command.Label;
					newButton.UseUnderline = true;
				}
				newButton.Clicked += ButtonClicked;
				buttons [n] = newButton;
				buttonBox.Append (newButton);
			}
			mainBox.Append (buttonBox);

			Child = mainBox;

			var keys = new Gtk.EventControllerKey ();
			keys.KeyPressed += (o, args) => {
				if (args.Keyval == (uint)Gdk.Key.Escape) {
					Visible = false;
					args.RetVal = true;
				}
			};
			AddController (keys);
		}

		static void ConfigureMessageLabel (Gtk.Label label)
		{
			label.Wrap = true;
			label.Xalign = 0;
			label.Halign = Gtk.Align.Start;
			label.Selectable = true;
			label.MaxWidthChars = 60;
		}

		public void FocusButton (int buttonNumber)
		{
			if (buttons.Length == 0)
				return;
			if (buttonNumber < 0 || buttonNumber >= buttons.Length)
				buttonNumber = buttons.Length - 1;
			DefaultWidget = buttons [buttonNumber];
			FocusWidget = buttons [buttonNumber];
		}

		void ButtonClicked (object sender, EventArgs e)
		{
			int i = Array.IndexOf (buttons, (Gtk.Button)sender);
			if (i >= 0)
				resultButton = commands [i];
			Visible = false;
		}
	}
}
