//
// DatePickerBackend.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//       Vsevolod Kukol <sevo@sevo.org>
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
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Xwt.Backends;


namespace Xwt.GtkBackend
{
	public class DatePickerBackend : WidgetBackend, IDatePickerBackend
	{
		public override void Initialize ()
		{
			Widget = new GtkDatePickerEntry ();
		}
		
		new GtkDatePickerEntry Widget {
			get { return (GtkDatePickerEntry)base.Widget; }
			set { base.Widget = value; }
		}
		
		protected new IDatePickerEventSink EventSink {
			get { return (IDatePickerEventSink)base.EventSink; }
		}
		
		public DateTime DateTime {
			get {
				return Widget.DateTime;
			}
			set {
				Widget.DateTime = value;
			}
		}

		public DateTime MinimumDateTime {
			get {
				return Widget.MinimumDateTime;
			}
			set {
				Widget.MinimumDateTime = value;
			}
		}

		public DateTime MaximumDateTime {
			get {
				return Widget.MaximumDateTime;
			}
			set {
				Widget.MaximumDateTime = value;
			}
		}

		public DatePickerStyle Style {
			get {
				return Widget.DatePickerStyle;
			}
			set {
				Widget.DatePickerStyle = value;
			}
		}
		
		public override void EnableEvent (object eventId)
		{
			base.EnableEvent (eventId);
			if (eventId is DatePickerEvent) {
				if ((DatePickerEvent)eventId == DatePickerEvent.ValueChanged)
					Widget.ValueChanged += HandleValueChanged;
			}
		}
		
		public override void DisableEvent (object eventId)
		{
			base.DisableEvent (eventId);
			if (eventId is DatePickerEvent) {
				if ((DatePickerEvent)eventId == DatePickerEvent.ValueChanged)
					Widget.ValueChanged -= HandleValueChanged;
			}
		}

		void HandleValueChanged (object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode (EventSink.ValueChanged);
		}
		
		/// <summary>
		/// Date/time entry: a Gtk.SpinButton whose text shows the formatted date and whose
		/// components (day, month, year, hour…) are selected and edited individually.
		/// GTK 4: GtkSpinButton has no overridable vfuncs any more, so this is a Gtk.Box
		/// hosting the spin button and driving it through its signals and event controllers.
		/// </summary>
		public class GtkDatePickerEntry : Gtk.Box
		{
			static string[] styleFormats = new string[3];

			static GtkDatePickerEntry ()
			{
				styleFormats[(int)DatePickerStyle.Date] = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
				// we use a custom static long time pattern, since we do not support 12/24 formats
				string timeSeparator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
				styleFormats[(int)DatePickerStyle.Time] = "HH" + timeSeparator + "mm" + timeSeparator + "ss";
				styleFormats[(int)DatePickerStyle.DateTime] = styleFormats[(int)DatePickerStyle.Date] + " " + styleFormats[(int)DatePickerStyle.Time];
			}

			readonly Gtk.SpinButton spin;
			Dictionary<DateTimeComponent, int> componentPosition = new Dictionary<DateTimeComponent, int>();
			Dictionary<DateTimeComponent, int> componentLength = new Dictionary<DateTimeComponent, int>();
			List<DateTimeComponent> componentsSorted = new List<DateTimeComponent>();

			public EventHandler ValueChanged;

			DateTimeComponent selectedComponent = DateTimeComponent.None;

			DateTime currentValue = DateTime.MinValue;

			/// <summary>The spin button that displays and edits the value.</summary>
			public Gtk.SpinButton SpinButton {
				get { return spin; }
			}

			Gtk.Adjustment Adjustment {
				get { return spin.Adjustment; }
			}

			public DateTime DateTime {
				get {
					return currentValue;
				}
				set {
					currentValue = value;
					Adjustment.Value = value.Ticks;
					RaiseChangedEvent ();
				}
			}

			public DateTime MinimumDateTime {
				get {
					return new DateTime ((long)Adjustment.Lower);
				}
				set {
					Adjustment.Lower = value.Ticks;
					if (DateTime < value)
						DateTime = value;
				}
			}

			public DateTime MaximumDateTime {
				get {
					return new DateTime ((long)Adjustment.Upper);
				}
				set {
					Adjustment.Upper = value.Ticks;
					if (DateTime > value)
						DateTime = value;
				}
			}

			DatePickerStyle style;
			public DatePickerStyle DatePickerStyle {
				get {
					return style;
				}
				set {
					style = value;
					string format = styleFormats [(int)value];

					componentPosition.Clear ();
					componentLength.Clear ();

					if (value == DatePickerStyle.DateTime ||
					    value == DatePickerStyle.Date) {
						componentPosition.Add (DateTimeComponent.Day, format.IndexOf ('d'));
						componentPosition.Add (DateTimeComponent.Month, format.IndexOf ('M'));
						componentPosition.Add (DateTimeComponent.Year, format.IndexOf ('y'));
						componentLength.Add (DateTimeComponent.Day, 2);
						componentLength.Add (DateTimeComponent.Month, 2);
						componentLength.Add (DateTimeComponent.Year, 4);
					}
					if (value == DatePickerStyle.DateTime ||
					    value == DatePickerStyle.Time) {
						componentPosition.Add (DateTimeComponent.Hour, format.IndexOfAny (new char[] { 'H', 'h' }));
						componentPosition.Add (DateTimeComponent.Minute, format.IndexOf ('m'));
						componentPosition.Add (DateTimeComponent.Second, format.IndexOf ('s'));
						componentLength.Add (DateTimeComponent.Hour, 2);
						componentLength.Add (DateTimeComponent.Minute, 2);
						componentLength.Add (DateTimeComponent.Second, 2);
					}

					componentsSorted = componentPosition.OrderBy (k => k.Value).Select (k => k.Key).ToList ();

					spin.WidthChars = format.Length;
					spin.Update ();
				}
			}

			public GtkDatePickerEntry () : this (DatePickerStyle.DateTime)
			{
			}

			public GtkDatePickerEntry (DatePickerStyle style)
			{
				spin = new Gtk.SpinButton (DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks, TimeSpan.TicksPerSecond);
				spin.Hexpand = true;
				spin.Numeric = false;
				Append (spin);

				spin.Output += HandleOutput;
				ConnectInput ();
				spin.Changed += HandleTextChanged;

				var focus = new Gtk.EventControllerFocus ();
				focus.Enter += HandleFocusEnter;
				focus.Leave += HandleFocusLeave;
				spin.AddController (focus);

				var click = new Gtk.GestureClick ();
				click.Button = 1;
				click.PropagationPhase = Gtk.PropagationPhase.Capture;
				click.Pressed += HandlePressed;
				spin.AddController (click);

				var keys = new Gtk.EventControllerKey ();
				keys.PropagationPhase = Gtk.PropagationPhase.Capture;
				keys.KeyPressed += HandleKeyPressed;
				keys.KeyReleased += HandleKeyReleased;
				spin.AddController (keys);

				DatePickerStyle = style;

				Adjustment.PageIncrement = TimeSpan.TicksPerDay;
				spin.IsEditable = true;
				DateTime = DateTime.Now;
				Adjustment.ValueChanged += HandleValueChanged;
			}

			void HandleValueChanged (object sender, EventArgs e)
			{
				if (Math.Abs (Adjustment.Value - currentValue.Ticks) < 1)
					return;

				if (selectedComponent == DateTimeComponent.None)
					SelectComponent (componentsSorted.Last ());

				if (Adjustment.Value > currentValue.Ticks)
					DateTime = currentValue.AddComponent (selectedComponent, 1);
				else if (Adjustment.Value < currentValue.Ticks)
					DateTime = currentValue.AddComponent (selectedComponent, -1);
			}

			void HandleOutput (object o, Gtk.OutputArgs args)
			{
				DateTime dateTime = new DateTime ((long)Adjustment.Value);
				string format = styleFormats [(int)DatePickerStyle];

				spin.Text = dateTime.ToString (format);
				args.RetVal = true;
			}

			// GtkSharp4 binds the "input" signal's gdouble* out parameter as a set-only IntPtr, so the
			// signal is connected natively: the text is never parsed, the value only changes through
			// component editing.
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
			delegate int InputCallback (IntPtr spinButton, IntPtr newValue, IntPtr data);
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
			delegate void DestroyCallback (IntPtr data, IntPtr closure);
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
			delegate IntPtr d_g_signal_connect_data (IntPtr instance, IntPtr detailed_signal, IntPtr handler, IntPtr data, IntPtr destroy_data, int connect_flags);

			static readonly d_g_signal_connect_data g_signal_connect_data = GtkInterop.LoadFunction<d_g_signal_connect_data> (NativeLib.GObject, "g_signal_connect_data");
			static readonly InputCallback inputCallback = OnInput;
			static readonly DestroyCallback destroyCallback = OnInputDestroyed;

			void ConnectInput ()
			{
				if (g_signal_connect_data == null)
					return;
				var data = GCHandle.ToIntPtr (GCHandle.Alloc (this, GCHandleType.Weak));
				var signal = GLib.Marshaller.StringToPtrGStrdup ("input");
				g_signal_connect_data (spin.Handle, signal, Marshal.GetFunctionPointerForDelegate (inputCallback), data,
					Marshal.GetFunctionPointerForDelegate (destroyCallback), 0);
				GLib.Marshaller.Free (signal);
			}

			static int OnInput (IntPtr spinButton, IntPtr newValue, IntPtr data)
			{
				try {
					var entry = GCHandle.FromIntPtr (data).Target as GtkDatePickerEntry;
					if (entry == null)
						return 0;
					Marshal.Copy (new [] { entry.Adjustment.Value }, 0, newValue, 1);
					return 1;
				} catch (Exception ex) {
					GLib.ExceptionManager.RaiseUnhandledException (ex, false);
					return 0;
				}
			}

			static void OnInputDestroyed (IntPtr data, IntPtr closure)
			{
				GCHandle.FromIntPtr (data).Free ();
			}

			void HandleFocusEnter (object sender, EventArgs e)
			{
				// Override default GTK behavior which is to select the whole entry on focus;
				// deferred because GtkText selects on focus after the controllers run
				GLib.Idle.Add (delegate {
					if (spin.Handle != IntPtr.Zero)
						SelectComponent (selectedComponent);
					return false;
				});
			}

			void HandleFocusLeave (object sender, EventArgs e)
			{
				SelectComponent (DateTimeComponent.None);
			}

			void HandleTextChanged (object sender, EventArgs e)
			{
				SelectComponent (selectedComponent);
			}

			void PasteFromClipboard ()
			{
				// GTK 4: the clipboard is read asynchronously
				var clipboard = spin.Clipboard;
				clipboard.ReadTextAsync (null, (source, res, data) => {
					string newText;
					try {
						newText = clipboard.ReadTextFinish (res);
					} catch (GLib.GException) {
						return;
					}
					if (newText == null)
						return;
					DateTime newDateTime;
					if (DateTime.TryParse (newText, out newDateTime))
						DateTime = newDateTime;
					else if (componentLength.ContainsKey (selectedComponent)
					         && componentLength[selectedComponent] == newText.Length) {
						int value;
						if (!int.TryParse (newText, out value))
							return;
						try {
							DateTime = DateTime.SetComponent (selectedComponent, value);
						} catch (ArgumentOutOfRangeException) {
						}
					}
				});
			}

			Gtk.Widget GetTextChild ()
			{
				for (var c = spin.FirstChild; c != null; c = c.NextSibling)
					if (c is Gtk.TextWidget)
						return c;
				return null;
			}

			void HandlePressed (object o, Gtk.PressedArgs args)
			{
				// handle clicks on the text part only (the +/- buttons are separate children)
				var text = GetTextChild ();
				var target = spin.Pick (args.X, args.Y, Gtk.PickFlags.Default);
				if (text == null || target == null || (target != text && !target.IsAncestor (text)))
					return;

				double tx, ty;
				if (!spin.TranslateCoordinates (text, args.X, args.Y, out tx, out ty))
					return;

				// GTK 4: GtkText does not expose its layout offsets; the date text is short and never scrolls
				var str = spin.Text ?? String.Empty;
				int index = 0, trailing;
				bool insideLayout;
				var layout = text.CreatePangoLayout (str);
				int lw, lh;
				layout.GetPixelSize (out lw, out lh);
				insideLayout = tx >= 0 && tx <= lw &&
					layout.XyToIndex (Pango.Units.FromPixels ((int)tx), Pango.Units.FromPixels (lh / 2), out index, out trailing);
				layout.Dispose ();

				if (insideLayout) {
					var bytes = System.Text.Encoding.UTF8.GetBytes (str);
					var charIndex = System.Text.Encoding.UTF8.GetCharCount (bytes, 0, Math.Min (index, bytes.Length));
					SelectComponentAtPosition (charIndex);
					((Gtk.GestureClick)o).SetState (Gtk.EventSequenceState.Claimed);
					if (!text.HasFocus)
						spin.GrabFocus ();
					return;
				}
				SelectComponent (DateTimeComponent.None);
			}

			int currentDigitInsert;

			void HandleKeyReleased (object o, Gtk.KeyReleasedArgs args)
			{
				HandleKeyReleased ((char)Gdk.Keyval.ToUnicode (args.Keyval));
			}

			void HandleKeyReleased (char pressedKey)
			{
				if (char.IsWhiteSpace(pressedKey) || char.IsPunctuation (pressedKey) || char.IsSeparator (pressedKey)) {
					if (pressedKey != '\t') // exclude tab
						SelectNextComponent ();
				}

				if (char.IsDigit (pressedKey) && selectedComponent != DateTimeComponent.None && pressedKey >= '0') {
					try {
						DateTime current = DateTime;
						current = current.SetComponent (selectedComponent, AddDigitToValue (current.GetComponent (selectedComponent), pressedKey));
						if (currentDigitInsert < componentLength[selectedComponent] - 1)
							currentDigitInsert++;
						else
							currentDigitInsert = 0;

						DateTime = current;
					} catch (ArgumentOutOfRangeException) {
						if (pressedKey == '0')
							return;
						if (currentDigitInsert != 0) {
							// In case date wasn't representable we redo the call with an updated digit insert
							currentDigitInsert = 0;
							HandleKeyReleased (pressedKey);
						}
					}
				}
			}

			int AddDigitToValue (int baseValue, char newValue)
			{
				if (currentDigitInsert == 0 || currentDigitInsert > componentLength [selectedComponent])
					return int.Parse (newValue.ToString ());
				return int.Parse (baseValue.ToString () + newValue);
			}

			void HandleKeyPressed (object o, Gtk.KeyPressedArgs args)
			{
				var key = (Gdk.Key)args.Keyval;
				if (key == Gdk.Key.Left || key == Gdk.Key.KP_Left) {
					SelectPrevComponent ();
					args.RetVal = true;
					return;
				}

				if (key == Gdk.Key.Right || key == Gdk.Key.KP_Right) {
					SelectNextComponent ();
					args.RetVal = true;
					return;
				}

				var xwtModifiers = args.State.ToXwtValue ();
				if ((key == Gdk.Key.v || key == Gdk.Key.V) && xwtModifiers.HasFlag (ModifierKeys.Control)) {
					// GTK 4: GtkText's paste-clipboard is not reachable from the spin button; paste is intercepted here
					PasteFromClipboard ();
					args.RetVal = true;
					return;
				}

				// We only allow the keypress to proceed to the normal handler
				// if it doesn't involve adding an actual character
				// (i.e. navigation keys)
				uint value = Gdk.Keyval.ToUnicode (args.Keyval);
				args.RetVal = !(value == 0 || value == '\t' || xwtModifiers.HasFlag (ModifierKeys.Control) || xwtModifiers.HasFlag (ModifierKeys.Alt));
			}

			void SelectComponentAtPosition (int characterIndex)
			{
				foreach (var entry in componentPosition) {
					if (characterIndex >= entry.Value  && characterIndex <= entry.Value + componentLength [entry.Key]) {
						SelectComponent (entry.Key);
						return;
					}
				}
				SelectComponent (DateTimeComponent.None);
			}

			void SelectComponent (DateTimeComponent component)
			{
				int startPos, endPos;
				if (componentPosition.ContainsKey (component) && componentLength.ContainsKey (component)) {
					startPos = componentPosition[component];
					endPos = startPos + componentLength[component];
				} else {
					startPos = spin.Position;
					endPos = spin.Position;
				}
				spin.SelectRegion (startPos, endPos);

				if (selectedComponent != component) {
					selectedComponent = component;
					currentDigitInsert = 0;
				}
			}

			void SelectNextComponent ()
			{
				if (selectedComponent == DateTimeComponent.None ||
				        componentsSorted.IndexOf (selectedComponent) == componentsSorted.Count - 1)
					selectedComponent = componentsSorted [0];
				else
					selectedComponent = componentsSorted [componentsSorted.IndexOf (selectedComponent) + 1];

				int startPos = componentPosition[selectedComponent];
				int endPos = startPos + componentLength[selectedComponent];
				spin.SelectRegion (startPos, endPos);
				currentDigitInsert = 0;
			}

			void SelectPrevComponent ()
			{
				if (selectedComponent == DateTimeComponent.None ||
				        componentsSorted.IndexOf (selectedComponent) == 0)
					selectedComponent = componentsSorted [componentsSorted.Count - 1];
				else
					selectedComponent = componentsSorted [componentsSorted.IndexOf (selectedComponent) - 1];

				int startPos = componentPosition[selectedComponent];
				int endPos = startPos + componentLength[selectedComponent];
				spin.SelectRegion (startPos, endPos);
				currentDigitInsert = 0;
			}

			void RaiseChangedEvent ()
			{
				var tmp = ValueChanged;
				if (tmp != null)
					tmp (this, EventArgs.Empty);
			}

			[Obsolete("Use DateTime property instead.")]
			public DateTime CurrentValue {
				get {
					return DateTime;
				}
				set {
					DateTime = value;
				}
			}
		}
	}

	enum DateTimeComponent {
		None = 0,
		Month,
		Day,
		Year,
		Hour,
		Minute,
		Second
	}

	static class DateTimeExtensions
	{
		public static DateTime AddComponent(this DateTime dateTime, DateTimeComponent component, int value)
		{
			try {
				switch (component) {
					case DateTimeComponent.Second:
						return dateTime.AddSeconds (value);
					case DateTimeComponent.Minute:
						return dateTime.AddMinutes (value);
					case DateTimeComponent.Hour:
						return dateTime.AddHours (value);
					case DateTimeComponent.Day:
						return dateTime.AddDays (value);
					case DateTimeComponent.Month:
						return dateTime.AddMonths (value);
					case DateTimeComponent.Year:
						return dateTime.AddYears (value);
					default:
						return dateTime.AddSeconds (value);
				}
			} catch (ArgumentOutOfRangeException) {
				return dateTime;
			}
		}

		public static int GetComponent(this DateTime dateTime, DateTimeComponent component)
		{
			switch (component) {
				case DateTimeComponent.Second:
					return dateTime.Second;
				case DateTimeComponent.Minute:
					return dateTime.Minute;
				case DateTimeComponent.Hour:
					return dateTime.Hour;
				case DateTimeComponent.Day:
					return dateTime.Day;
				case DateTimeComponent.Month:
					return dateTime.Month;
				case DateTimeComponent.Year:
					return dateTime.Year;
				default:
					return 0;
			}
		}

		public static DateTime SetComponent (this DateTime source, DateTimeComponent component, int newValue)
		{
			int year = source.Year;
			int month = source.Month;
			int day = source.Day;
			int hour = source.Hour;
			int minute = source.Minute;
			int second = source.Second;
			switch (component) {
				case DateTimeComponent.Year:
					return new DateTime (newValue, month, day, hour, minute, second);
				case DateTimeComponent.Month:
					return new DateTime (year, newValue, day, hour, minute, second);
				case DateTimeComponent.Day:
					return new DateTime (year, month, newValue, hour, minute, second);
				case DateTimeComponent.Hour:
					return new DateTime (year, month, day, newValue, minute, second);
				case DateTimeComponent.Minute:
					return new DateTime (year, month, day, hour, newValue, second);
				case DateTimeComponent.Second:
					return new DateTime (year, month, day, hour, minute, newValue);
				default:
					return source;
			}
		}
	}
}

