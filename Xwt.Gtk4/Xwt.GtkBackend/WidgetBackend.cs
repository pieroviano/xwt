// 
// WidgetBackend.cs
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
using Xwt;
using System.Collections.Generic;
using System.Linq;

using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	/// <summary>
	/// Base of every Xwt GTK 4 widget backend. Input is delivered through event controllers
	/// attached lazily to <see cref="EventsRootWidget"/> (GTK 4 has no per-widget event signals,
	/// event masks nor event boxes), styling through a per-widget CSS provider.
	/// </summary>
	public partial class WidgetBackend: IWidgetBackend, IGtkWidgetBackend
	{
		Gtk.Widget widget;
		Widget frontend;
		IWidgetEventSink eventSink;
		WidgetEvent enabledEvents;
		bool destroyed;
		bool minSizeSet;

		// lazily created event controllers
		Gtk.EventControllerLegacy buttonController;
		bool buttonPressEnabled, buttonReleaseEnabled;
		uint lastPressButton, lastPressTime;
		Point lastPressPosition;
		int pressCount;
		Gtk.EventControllerMotion motionController;
		Gtk.EventControllerKey keyController;        // capture phase: events targeted at Widget itself
		Gtk.EventControllerKey bubbleKeyController;  // bubble phase: events coming from children
		Gtk.EventControllerScroll scrollController;
		Gtk.EventControllerFocus focusController;
		Gtk.DragSource dragSource;
		Gtk.DropTargetAsync dropTarget;
		Point lastPointerPosition;

		class DragDropData
		{
			public TransferDataSource CurrentDragData;
			public Gdk.DragAction DestDragAction;
			public Gdk.DragAction SourceDragAction;
			public TransferDataStore DragData;
			public TransferDataType[] ValidDropTypes;
			public Point LastDragPosition;
			public Gdk.DragAction LastStatus;
			public int Requests;
		}

		DragDropData dragDropInfo;

		const WidgetEvent dragDropEvents = WidgetEvent.DragDropCheck | WidgetEvent.DragDrop | WidgetEvent.DragOver | WidgetEvent.DragOverCheck;

		void IBackend.InitializeBackend (object frontend, ApplicationContext context)
		{
			this.frontend = (Widget) frontend;
			ApplicationContext = context;
		}

		void IWidgetBackend.Initialize (IWidgetEventSink sink)
		{
			eventSink = sink;
			Initialize ();
		}

		public virtual void Initialize ()
		{
		}

		public IWidgetEventSink EventSink {
			get { return eventSink; }
		}

		public Widget Frontend {
			get { return frontend; }
		}

		public ApplicationContext ApplicationContext {
			get;
			private set;
		}

		public object NativeWidget {
			get {
				return RootWidget;
			}
		}

		public Gtk.Widget Widget {
			get { return widget; }
			set {
				if (widget != null) {
					value.Visible = widget.Visible;
					GtkEngine.ReplaceChild (widget, value);
					MoveControllers (widget, value);
				}
				widget = value;
			}
		}

		void MoveControllers (Gtk.Widget from, Gtk.Widget to)
		{
			foreach (var c in new Gtk.EventController[] { buttonController, motionController, keyController, bubbleKeyController, scrollController, focusController, dragSource, dropTarget }) {
				if (c != null && c.Widget == from) {
					from.RemoveController (c);
					to.AddController (c);
				}
			}
		}

		/// <summary>
		/// The widget that is inserted in the parent container: the backend widget itself,
		/// or the placement wrapper that applies the frontend margins and alignment.
		/// </summary>
		public Gtk.Widget RootWidget {
			get {
				return Widget;
			}
		}

		public string Name {
			get { return Widget.Name; }
			set { Widget.Name = value; }
		}

		public virtual bool Visible {
			get { return Widget.Visible; }
			set { Widget.Visible = value; }
		}

		void RunWhenRealized (Action a)
		{
			if (Widget.IsRealized)
				a ();
			else {
				EventHandler h = null;
				h = delegate {
					EventsRootWidget.Realized -= h;
					a ();
				};
				EventsRootWidget.Realized += h;
			}
		}

		public virtual bool Sensitive {
			get { return Widget.Sensitive; }
			set { Widget.Sensitive = value; }
		}

		public bool CanGetFocus {
			get { return Widget.Focusable; }
			set { Widget.Focusable = value; }
		}

		public bool HasFocus {
			get { return Widget.IsFocus; }
		}

		public virtual void SetFocus ()
		{
			if (CanGetFocus)
				Widget.GrabFocus ();
		}

		public string TooltipText {
			get {
				return Widget.TooltipText;
			}
			set {
				Widget.TooltipText = value;
			}
		}

		static Dictionary<CursorType,Gdk.Cursor> gtkCursors = new Dictionary<CursorType, Gdk.Cursor> ();

		internal CursorType CurrentCursor { get; private set; }

		internal static string GetCursorName (CursorType cursor)
		{
			if (cursor == CursorType.Arrow)
				return "default";
			if (cursor == CursorType.Crosshair)
				return "crosshair";
			if (cursor == CursorType.Hand || cursor == CursorType.Hand2)
				return "pointer";
			if (cursor == CursorType.DragCopy)
				return "copy";
			if (cursor == CursorType.IBeam)
				return "text";
			if (cursor == CursorType.ResizeDown)
				return "s-resize";
			if (cursor == CursorType.ResizeUp)
				return "n-resize";
			if (cursor == CursorType.ResizeLeft)
				return "w-resize";
			if (cursor == CursorType.ResizeRight)
				return "e-resize";
			if (cursor == CursorType.ResizeLeftRight)
				return "ew-resize";
			if (cursor == CursorType.ResizeUpDown)
				return "ns-resize";
			if (cursor == CursorType.ResizeNE)
				return "ne-resize";
			if (cursor == CursorType.ResizeNW)
				return "nw-resize";
			if (cursor == CursorType.ResizeSE)
				return "se-resize";
			if (cursor == CursorType.ResizeSW)
				return "sw-resize";
			if (cursor == CursorType.NotAllowed)
				return "not-allowed";
			if (cursor == CursorType.Move)
				return "move";
			if (cursor == CursorType.Wait)
				return "wait";
			if (cursor == CursorType.Help)
				return "help";
			if (cursor == CursorType.Invisible)
				return "none";
			return "default";
		}

		internal static Gdk.Cursor GetCursor (CursorType cursor)
		{
			Gdk.Cursor gc;
			if (!gtkCursors.TryGetValue (cursor, out gc))
				gtkCursors [cursor] = gc = new Gdk.Cursor (GetCursorName (cursor), null);
			return gc;
		}

		public void SetCursor (CursorType cursor)
		{
			// GTK 4 applies the widget cursor while the pointer is over it; no enter/leave tracking needed
			CurrentCursor = cursor;
			EventsRootWidget.Cursor = GetCursor (cursor);
		}

		~WidgetBackend ()
		{
			Dispose (false);
		}

		public void Dispose ()
		{
			GC.SuppressFinalize (this);
			Dispose (true);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (Widget != null && disposing && !destroyed) {
				MarkDestroyed (Frontend);
				var root = RootWidget;
				if (root is Gtk.Window)
					((Gtk.Window)root).Destroy ();
				else if (root.Parent != null)
					GtkEngine.RemoveChild (root);
			}
			if (IMContext != null)
				IMContext.Dispose ();
		}

		void MarkDestroyed (Widget w)
		{
			var wbk = Toolkit.GetBackend (w);
			var bk = wbk as WidgetBackend;
			if (bk == null) {
				var ew = wbk as Xwt.Widget;
				if (ew == null)
					return;
				bk = Toolkit.GetBackend (ew) as WidgetBackend;
				if (bk == null)
					return;
			}
			bk.destroyed = true;
			foreach (var c in w.Surface.Children)
				MarkDestroyed (c);
		}

		public virtual Size Size {
			get {
				return new Size (Widget.Width, Widget.Height);
			}
		}

		public void SetSizeConstraints (SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
		}

		DragDropData DragDropInfo {
			get {
				if (dragDropInfo == null)
					dragDropInfo = new DragDropData ();
				return dragDropInfo;
			}
		}

		public Point ConvertToParentCoordinates (Point widgetCoordinates)
		{
			double x = 0, y = 0;
			var parent = RootWidget?.Parent;
			if (parent != null)
				Widget.TranslateCoordinates (parent, 0, 0, out x, out y);
			return new Point (x + widgetCoordinates.X, y + widgetCoordinates.Y);
		}

		public Point ConvertToWindowCoordinates (Point widgetCoordinates)
		{
			double x = 0, y = 0;
			var root = Widget.Root as Gtk.Widget;
			if (root != null)
				Widget.TranslateCoordinates (root, 0, 0, out x, out y);
			return new Point (x + widgetCoordinates.X, y + widgetCoordinates.Y);
		}

		public Point ConvertToScreenCoordinates (Point widgetCoordinates)
		{
			return GtkWorkarounds.ConvertToScreenCoordinates (Widget, widgetCoordinates);
		}

		Pango.FontDescription customFont;

		public virtual object Font {
			get {
				return customFont ?? Widget.PangoContext.FontDescription;
			}
			set {
				var fd = (Pango.FontDescription) value;
				customFont = fd;
				Widget.SetStyle ("font", fd != null ? fd.ToCssValue () : null);
			}
		}

		Color? customBackgroundColor;

		public virtual Color BackgroundColor {
			get {
				return customBackgroundColor.HasValue ? customBackgroundColor.Value : Widget.GetBackgroundColor ();
			}
			set {
				customBackgroundColor = value;
				OnSetBackgroundColor (value);
			}
		}

		public virtual bool UsingCustomBackgroundColor {
			get { return customBackgroundColor.HasValue; }
		}

		protected virtual void OnSetBackgroundColor (Color color)
		{
			Widget.SetBackgroundColor (color);
		}

		Gtk.Widget IGtkWidgetBackend.Widget {
			get { return RootWidget; }
		}

		/// <summary>
		/// The widget the event controllers are attached to.
		/// </summary>
		protected virtual Gtk.Widget EventsRootWidget {
			get { return Widget; }
		}

		protected Gtk.IEditable EditableWidget {
			get { return Widget as Gtk.IEditable; }
		}

		public static Gtk.Widget GetWidget (IWidgetBackend w)
		{
			return w != null ? ((IGtkWidgetBackend)w).Widget : null;
		}

		public virtual void UpdateChildPlacement (IWidgetBackend childBackend)
		{
			SetChildPlacement (childBackend);
		}

		/// <summary>
		/// Returns the widget to insert in a single-child container: the child itself, or a
		/// <see cref="WidgetPlacementWrapper"/> applying the frontend margins and alignment.
		/// </summary>
		public static Gtk.Widget GetWidgetWithPlacement (IWidgetBackend childBackend)
		{
			var backend = (WidgetBackend)childBackend;
			var child = backend.RootWidget;
			var wrapper = child.Parent as WidgetPlacementWrapper;
			if (wrapper != null)
				return wrapper;

			if (!NeedsAlignmentWrapper (backend.Frontend))
				return child;

			wrapper = new WidgetPlacementWrapper ();
			wrapper.Child = child;
			wrapper.UpdatePlacement (backend.Frontend);
			return wrapper;
		}

		/// <summary>
		/// Detaches the child from its placement wrapper (if <paramref name="w"/> is one), so the
		/// child can be re-parented.
		/// </summary>
		public static void RemoveChildPlacement (Gtk.Widget w)
		{
			var wp = w as WidgetPlacementWrapper;
			if (wp != null)
				wp.Child = null;
		}

		static bool NeedsAlignmentWrapper (Widget fw)
		{
			return fw.HorizontalPlacement != WidgetPlacement.Fill || fw.VerticalPlacement != WidgetPlacement.Fill || fw.Margin.VerticalSpacing != 0 || fw.Margin.HorizontalSpacing != 0;
		}

		public static void SetChildPlacement (IWidgetBackend childBackend)
		{
			var backend = (WidgetBackend)childBackend;
			var child = backend.RootWidget;
			var wrapper = child.Parent as WidgetPlacementWrapper;
			var fw = backend.Frontend;

			if (!NeedsAlignmentWrapper (fw)) {
				if (wrapper != null) {
					var parent = wrapper.Parent;
					wrapper.Child = null;
					if (parent != null)
						GtkEngine.ReplaceChild (wrapper, child);
				}
				return;
			}

			if (wrapper == null) {
				wrapper = new WidgetPlacementWrapper ();
				if (child.Parent != null)
					GtkEngine.ReplaceChild (child, wrapper);
				wrapper.Child = child;
			}
			wrapper.UpdatePlacement (fw);
		}

		public virtual void UpdateLayout ()
		{
			Widget.QueueResize ();
		}

		#region Size negotiation

		public virtual Size GetPreferredSize (SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
			SetSizeConstraints (widthConstraint, heightConstraint);

			int min_width = 0;
			int min_height = 0;

			IWidgetBackend xwt_backend = Widget as IWidgetBackend;
			if (xwt_backend != null) {
				var size = xwt_backend.GetPreferredSize (widthConstraint, heightConstraint);
				min_width = (int)size.Width;
				min_height = (int)size.Height;
			} else {
				int nat, minb, natb;
				if (Widget.RequestMode == Gtk.SizeRequestMode.WidthForHeight) {
					int forHeight = heightConstraint.IsConstrained ? (int)heightConstraint.AvailableSize : -1;
					Widget.Measure (Gtk.Orientation.Vertical, -1, out min_height, out nat, out minb, out natb);
					if (forHeight < 0)
						forHeight = min_height;
					Widget.Measure (Gtk.Orientation.Horizontal, forHeight, out min_width, out nat, out minb, out natb);
				} else {
					int forWidth = widthConstraint.IsConstrained ? (int)widthConstraint.AvailableSize : -1;
					Widget.Measure (Gtk.Orientation.Horizontal, -1, out min_width, out nat, out minb, out natb);
					if (forWidth < 0)
						forWidth = min_width;
					Widget.Measure (Gtk.Orientation.Vertical, forWidth, out min_height, out nat, out minb, out natb);
				}
			}

			if ((enabledEvents & WidgetEvent.PreferredSizeCheck) != 0) {
				SizeConstraint wc = SizeConstraint.Unconstrained, hc = SizeConstraint.Unconstrained;
				var cp = Widget.Parent as IConstraintProvider;
				if (cp != null)
					cp.GetConstraints (Widget, out wc, out hc);

				ApplicationContext.InvokeUserCode (delegate {
					var w = eventSink.GetPreferredSize (wc, hc);
					min_width = (int) w.Width;
					min_height = (int) w.Height;
				});
			}

			if (Widget.WidthRequest > min_width)
				min_width = Widget.WidthRequest;
			if (Widget.HeightRequest > min_height)
				min_height = Widget.HeightRequest;

			if (Frontend.MinWidth > 0 && Frontend.MinWidth > min_width)
				min_width = (int) Frontend.MinWidth;

			if (Frontend.MinHeight > 0 && Frontend.MinHeight > min_height)
				min_height = (int) Frontend.MinHeight;

			return new Size (min_width, min_height);
		}

		void UpdateSizeRequests ()
		{
			if (minSizeSet) {
				Widget.WidthRequest = (int)Math.Max (Frontend.MinWidth, Frontend.WidthRequest);
				Widget.HeightRequest = (int)Math.Max (Frontend.MinHeight, Frontend.HeightRequest);
			} else {
				Widget.WidthRequest = (int)Frontend.WidthRequest;
				Widget.HeightRequest = (int)Frontend.HeightRequest;
			}
		}

		public void SetMinSize (double width, double height)
		{
			minSizeSet = width != -1 || height != -1;
			UpdateSizeRequests ();
			Widget.QueueResize ();
		}

		public void SetSizeRequest (double width, double height)
		{
			UpdateSizeRequests ();
		}

		public double Opacity {
			get { return Widget.Opacity; }
			set { Widget.Opacity = value; }
		}

		#endregion

		#region Event controllers

		// Button events go through a capture-phase legacy controller: a GestureClick here would be
		// denied (and lose the release) whenever the widget's own gestures claim the sequence, e.g.
		// a TreeView row click. This reproduces the GTK 2/3 "connect before" button handlers.
		void EnsureButtonController ()
		{
			if (buttonController == null) {
				buttonController = new Gtk.EventControllerLegacy ();
				buttonController.PropagationPhase = Gtk.PropagationPhase.Capture;
				LegacyEvents.Connect (buttonController, HandleButtonEvent);
				EventsRootWidget.AddController (buttonController);
			}
		}

		Gtk.EventControllerMotion MotionController {
			get {
				if (motionController == null) {
					motionController = new Gtk.EventControllerMotion ();
					motionController.Motion += TrackPointerPosition;
					motionController.Enter += TrackPointerEnter;
					EventsRootWidget.AddController (motionController);
				}
				return motionController;
			}
		}

		void TrackPointerPosition (object o, Gtk.MotionArgs args)
		{
			lastPointerPosition = new Point (args.X, args.Y);
		}

		void TrackPointerEnter (object o, Gtk.EnterArgs args)
		{
			lastPointerPosition = new Point (args.X, args.Y);
		}

		Gtk.EventControllerKey KeyController {
			get {
				if (keyController == null) {
					// Capture phase on the widget itself runs before the widget's own key handling,
					// matching the GTK 2/3 "connect before" semantics for events targeted at Widget.
					keyController = new Gtk.EventControllerKey ();
					keyController.PropagationPhase = Gtk.PropagationPhase.Capture;
					keyController.KeyPressed += HandleCaptureKeyPressed;
					keyController.KeyReleased += HandleCaptureKeyReleased;
					Widget.AddController (keyController);

					// Bubble phase: key events that a focused descendant did not handle.
					bubbleKeyController = new Gtk.EventControllerKey ();
					bubbleKeyController.KeyPressed += HandleBubbleKeyPressed;
					bubbleKeyController.KeyReleased += HandleBubbleKeyReleased;
					Widget.AddController (bubbleKeyController);
				}
				return keyController;
			}
		}

		Gtk.EventControllerScroll ScrollController {
			get {
				if (scrollController == null) {
					scrollController = new Gtk.EventControllerScroll (Gtk.EventControllerScrollFlags.BothAxes);
					EventsRootWidget.AddController (scrollController);
					// scroll events carry no position; the motion controller tracks it
					var m = MotionController;
				}
				return scrollController;
			}
		}

		Gtk.EventControllerFocus FocusController {
			get {
				if (focusController == null) {
					focusController = new Gtk.EventControllerFocus ();
					Widget.AddController (focusController);
				}
				return focusController;
			}
		}

		long CurrentEventTime (Gtk.EventController controller)
		{
			return controller != null ? (long)controller.CurrentEventTime : 0;
		}

		public virtual void EnableEvent (object eventId)
		{
			if (eventId is WidgetEvent) {
				WidgetEvent ev = (WidgetEvent) eventId;
				switch (ev) {
				case WidgetEvent.DragLeave:
				case WidgetEvent.DragStarted:
					// handled by the drag source / drop target controllers
					break;
				case WidgetEvent.KeyPressed:
				case WidgetEvent.KeyReleased:
					var k = KeyController;
					break;
				case WidgetEvent.GotFocus:
					FocusController.Enter += HandleWidgetFocusInEvent;
					break;
				case WidgetEvent.LostFocus:
					FocusController.Leave += HandleWidgetFocusOutEvent;
					break;
				case WidgetEvent.MouseEntered:
					MotionController.Enter += HandleEnterNotifyEvent;
					break;
				case WidgetEvent.MouseExited:
					MotionController.Leave += HandleLeaveNotifyEvent;
					break;
				case WidgetEvent.ButtonPressed:
					EnsureButtonController ();
					buttonPressEnabled = true;
					break;
				case WidgetEvent.ButtonReleased:
					EnsureButtonController ();
					buttonReleaseEnabled = true;
					break;
				case WidgetEvent.MouseMoved:
					MotionController.Motion += HandleMotionNotifyEvent;
					break;
				case WidgetEvent.BoundsChanged:
					EnableBoundsChanged ();
					break;
				case WidgetEvent.MouseScrolled:
					ScrollController.Scroll += HandleScrollEvent;
					break;
				case WidgetEvent.TextInput:
					if (EditableWidget != null) {
						EditableWidget.TextInserted += HandleTextInserted;
					} else {
						if (IMContext == null) {
							IMContext = new Gtk.IMMulticontext ();
							IMContext.ClientWidget = Widget;
							IMContext.Commit += HandleImCommitEvent;
						}
						// the key controller filters key events through the input method
						KeyController.ImContext = IMContext;
					}
					break;
				}
				enabledEvents |= ev;
			}
		}

		public virtual void DisableEvent (object eventId)
		{
			if (eventId is WidgetEvent) {
				WidgetEvent ev = (WidgetEvent) eventId;
				switch (ev) {
				case WidgetEvent.GotFocus:
					if (focusController != null)
						focusController.Enter -= HandleWidgetFocusInEvent;
					break;
				case WidgetEvent.LostFocus:
					if (focusController != null)
						focusController.Leave -= HandleWidgetFocusOutEvent;
					break;
				case WidgetEvent.MouseEntered:
					if (motionController != null)
						motionController.Enter -= HandleEnterNotifyEvent;
					break;
				case WidgetEvent.MouseExited:
					if (motionController != null)
						motionController.Leave -= HandleLeaveNotifyEvent;
					break;
				case WidgetEvent.ButtonPressed:
					buttonPressEnabled = false;
					break;
				case WidgetEvent.ButtonReleased:
					buttonReleaseEnabled = false;
					break;
				case WidgetEvent.MouseMoved:
					if (motionController != null)
						motionController.Motion -= HandleMotionNotifyEvent;
					break;
				case WidgetEvent.BoundsChanged:
					DisableBoundsChanged ();
					break;
				case WidgetEvent.MouseScrolled:
					if (scrollController != null)
						scrollController.Scroll -= HandleScrollEvent;
					break;
				case WidgetEvent.TextInput:
					if (EditableWidget != null) {
						EditableWidget.TextInserted -= HandleTextInserted;
					} else if (IMContext != null) {
						IMContext.Commit -= HandleImCommitEvent;
						if (keyController != null)
							keyController.ImContext = null;
						IMContext.Dispose ();
						IMContext = null;
					}
					break;
				}

				enabledEvents &= ~ev;
			}
		}

		#endregion

		#region Bounds changed

		// GTK 4 has no size-allocate signal: the allocation is checked after each frame's layout.
		Gdk.FrameClock boundsClock;
		Rectangle lastBounds;

		void EnableBoundsChanged ()
		{
			Widget.Realized += HandleRealizedForBounds;
			Widget.Unrealized += HandleUnrealizedForBounds;
			if (Widget.IsRealized)
				HandleRealizedForBounds (Widget, EventArgs.Empty);
		}

		void DisableBoundsChanged ()
		{
			Widget.Realized -= HandleRealizedForBounds;
			Widget.Unrealized -= HandleUnrealizedForBounds;
			HandleUnrealizedForBounds (Widget, EventArgs.Empty);
		}

		void HandleRealizedForBounds (object sender, EventArgs e)
		{
			if (boundsClock != null)
				return;
			boundsClock = Widget.FrameClock;
			if (boundsClock != null)
				boundsClock.Layout += HandleFrameLayout;
		}

		void HandleUnrealizedForBounds (object sender, EventArgs e)
		{
			if (boundsClock != null) {
				boundsClock.Layout -= HandleFrameLayout;
				boundsClock = null;
			}
		}

		[GLib.ConnectBefore]
		void HandleFrameLayout (object sender, EventArgs e)
		{
			// queued so it runs after the layout phase allocated the widget
			GLib.Idle.Add (CheckBoundsChanged);
		}

		bool CheckBoundsChanged ()
		{
			if (Widget == null || destroyed)
				return false;
			double x = 0, y = 0;
			var parent = Widget.Parent;
			if (parent != null)
				Widget.TranslateCoordinates (parent, 0, 0, out x, out y);
			var bounds = new Rectangle (x, y, Widget.Width, Widget.Height);
			if (bounds != lastBounds) {
				lastBounds = bounds;
				ApplicationContext.InvokeUserCode (EventSink.OnBoundsChanged);
			}
			return false;
		}

		/// <summary>
		/// Raises BoundsChanged immediately. Backends whose widget allocates itself (custom
		/// containers) call this from their size_allocate override.
		/// </summary>
		protected void NotifyBoundsChanged ()
		{
			if ((enabledEvents & WidgetEvent.BoundsChanged) != 0)
				CheckBoundsChanged ();
		}

		#endregion

		#region Keyboard

		void HandleCaptureKeyPressed (object o, Gtk.KeyPressedArgs args)
		{
			// only events whose target is Widget itself; descendants are handled while bubbling
			if (!Widget.IsFocus)
				return;
			if (OnKeyPressed (args))
				args.RetVal = true;
		}

		void HandleBubbleKeyPressed (object o, Gtk.KeyPressedArgs args)
		{
			if (Widget.IsFocus)
				return; // already seen in the capture phase
			if (OnKeyPressed (args))
				args.RetVal = true;
		}

		void HandleCaptureKeyReleased (object o, Gtk.KeyReleasedArgs args)
		{
			if (Widget.IsFocus)
				OnKeyReleased (args);
		}

		void HandleBubbleKeyReleased (object o, Gtk.KeyReleasedArgs args)
		{
			if (!Widget.IsFocus)
				OnKeyReleased (args);
		}

		bool OnKeyPressed (Gtk.KeyPressedArgs args)
		{
			bool handled = false;
			if ((enabledEvents & WidgetEvent.KeyPressed) != 0) {
				KeyEventArgs kargs = GetKeyPressEventArgs (args);
				if (kargs != null) {
					ApplicationContext.InvokeUserCode (delegate {
						EventSink.OnKeyPressed (kargs);
					});
					handled = kargs.Handled;
				}
			}
			if (!handled && (enabledEvents & WidgetEvent.TextInput) != 0 && EditableWidget == null) {
				// new lines are not committed by the input method, handle them here
				var key = (Gdk.Key)args.Keyval;
				if (key == Gdk.Key.Return || key == Gdk.Key.ISO_Enter || key == Gdk.Key.KP_Enter) {
					var pargs = new TextInputEventArgs (Environment.NewLine);
					ApplicationContext.InvokeUserCode (delegate {
						EventSink.OnTextInput (pargs);
					});
					handled = pargs.Handled;
				}
			}
			return handled;
		}

		void OnKeyReleased (Gtk.KeyReleasedArgs args)
		{
			if ((enabledEvents & WidgetEvent.KeyReleased) == 0)
				return;
			KeyEventArgs kargs = GetKeyReleaseEventArgs (args);
			if (kargs == null)
				return;
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnKeyReleased (kargs);
			});
		}

		protected virtual KeyEventArgs GetKeyReleaseEventArgs (Gtk.KeyReleasedArgs args)
		{
			Key k = (Key)args.Keyval;
			ModifierKeys m = args.State.ToXwtValue ();
			return new KeyEventArgs (k, (int)args.Keyval, m, false, CurrentEventTime (keyController));
		}

		protected virtual KeyEventArgs GetKeyPressEventArgs (Gtk.KeyPressedArgs args)
		{
			Key k = (Key)args.Keyval;
			ModifierKeys m = args.State.ToXwtValue ();
			return new KeyEventArgs (k, (int)args.Keyval, m, false, CurrentEventTime (keyController));
		}

		protected Gtk.IMContext IMContext { get; set; }

		[GLib.ConnectBefore]
		void HandleTextInserted (object o, Gtk.TextInsertedArgs args)
		{
			if (String.IsNullOrEmpty (args.Text))
				return;

			var pargs = new TextInputEventArgs (args.Text);
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnTextInput (pargs);
			});

			if (pargs.Handled)
				((GLib.Object)o).StopSignal ("insert-text");
		}

		void HandleImCommitEvent (object o, Gtk.CommitArgs args)
		{
			if (String.IsNullOrEmpty (args.Str))
				return;

			var pargs = new TextInputEventArgs (args.Str);
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnTextInput (pargs);
			});
		}

		#endregion

		#region Pointer

		void HandleScrollEvent (object o, Gtk.ScrollArgs args)
		{
			var a = GetScrollEventArgs (args, lastPointerPosition);
			if (a == null)
				return;
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnMouseScrolled (a);
			});
			if (a.Handled)
				args.RetVal = true;
		}

		protected virtual MouseScrolledEventArgs GetScrollEventArgs (Gtk.ScrollArgs args, Point position)
		{
			ScrollDirection direction;
			if (Math.Abs (args.Dy) >= Math.Abs (args.Dx))
				direction = args.Dy < 0 ? ScrollDirection.Up : ScrollDirection.Down;
			else
				direction = args.Dx < 0 ? ScrollDirection.Left : ScrollDirection.Right;
			return new MouseScrolledEventArgs (CurrentEventTime (scrollController), position.X, position.Y, direction);
		}

		void HandleWidgetFocusOutEvent (object o, EventArgs args)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnLostFocus);
		}

		void HandleWidgetFocusInEvent (object o, EventArgs args)
		{
			if (!CanGetFocus)
				return;
			ApplicationContext.InvokeUserCode (EventSink.OnGotFocus);
		}

		void HandleLeaveNotifyEvent (object o, EventArgs args)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnMouseExited);
		}

		void HandleEnterNotifyEvent (object o, Gtk.EnterArgs args)
		{
			ApplicationContext.InvokeUserCode (EventSink.OnMouseEntered);
		}

		void HandleMotionNotifyEvent (object o, Gtk.MotionArgs args)
		{
			var a = GetMouseMovedEventArgs (args);
			if (a == null)
				return;
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnMouseMoved (a);
			});
		}

		protected virtual MouseMovedEventArgs GetMouseMovedEventArgs (Gtk.MotionArgs args)
		{
			return new MouseMovedEventArgs (CurrentEventTime (motionController), args.X, args.Y);
		}

		bool GetEventPosition (Gdk.Event evt, out double x, out double y)
		{
			x = y = 0;
			double ex, ey, sx, sy;
			var widget = EventsRootWidget;
			var native = widget.Native;
			var nativeWidget = widget.GetNativeWidget ();
			if (native == null || nativeWidget == null || !evt.GetPosition (out ex, out ey))
				return false;
			// event coordinates are relative to the surface, which is offset from the native widget
			native.GetSurfaceTransform (out sx, out sy);
			return nativeWidget.TranslateCoordinates (widget, ex - sx, ey - sy, out x, out y);
		}

		bool HandleButtonEvent (Gdk.Event evt)
		{
			var type = evt.EventType;
			bool press = type == Gdk.EventType.ButtonPress;
			if (!press && type != Gdk.EventType.ButtonRelease)
				return false;
			if (press ? !buttonPressEnabled : !buttonReleaseEnabled)
				return false;
			double x, y;
			if (!GetEventPosition (evt, out x, out y))
				return false;
			var button = new Gdk.ButtonEvent (evt.Handle).Button;
			var state = evt.ModifierState;

			if (press) {
				// GTK 4 delivers no 2BUTTON/3BUTTON_PRESS events: count the presses here
				var settings = Gtk.Settings.Default;
				int time = settings != null ? settings.GtkDoubleClickTime : 400;
				int distance = settings != null ? settings.GtkDoubleClickDistance : 5;
				if (button == lastPressButton && evt.Time - lastPressTime <= time
				    && Math.Abs (x - lastPressPosition.X) <= distance && Math.Abs (y - lastPressPosition.Y) <= distance)
					pressCount++;
				else
					pressCount = 1;
				lastPressButton = button;
				lastPressTime = evt.Time;
				lastPressPosition = new Point (x, y);

				var a = GetButtonPressEventArgs (button, pressCount, x, y, state);
				if (a == null)
					return false;
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnButtonPressed (a);
				});
				return a.Handled;
			} else {
				var a = GetButtonReleaseEventArgs (button, pressCount, x, y, state);
				if (a == null)
					return false;
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnButtonReleased (a);
				});
				return a.Handled;
			}
		}

		protected virtual ButtonEventArgs GetButtonReleaseEventArgs (uint button, int nPress, double x, double y, Gdk.ModifierType state)
		{
			var a = new ButtonEventArgs ();
			a.X = x;
			a.Y = y;
			a.Button = (PointerButton) button;
			a.MultiplePress = nPress;
			return a;
		}

		protected virtual ButtonEventArgs GetButtonPressEventArgs (uint button, int nPress, double x, double y, Gdk.ModifierType state)
		{
			var a = new ButtonEventArgs ();
			a.X = x;
			a.Y = y;
			a.Button = (PointerButton) button;
			a.MultiplePress = nPress;
			a.IsContextMenuTrigger = GtkWorkarounds.TriggersContextMenu (button, state);
			return a;
		}

		#endregion

		#region Drag and drop

		Gtk.DropTargetAsync DropTarget {
			get { return dropTarget; }
		}

		internal Gdk.DragAction DoDragMotion (Gdk.Drop drop, double x, double y)
		{
			DragDropInfo.LastDragPosition = new Point (x, y);

			DragDropAction ac;
			if ((enabledEvents & WidgetEvent.DragOverCheck) == 0) {
				if ((enabledEvents & WidgetEvent.DragOver) != 0)
					ac = DragDropAction.Default;
				else
					ac = ConvertDragAction (DragDropInfo.DestDragAction);
			}
			else {
				var cact = ConvertDragAction (drop.Actions);
				if (cact == DragDropAction.All)
					cact = DragDropAction.Move;

				var targetTypes = Util.GetDragTypes (drop.Formats);
				DragOverCheckEventArgs da = new DragOverCheckEventArgs (new Point (x, y), targetTypes, cact);
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnDragOverCheck (da);
				});
				ac = da.AllowedAction;
				if ((enabledEvents & WidgetEvent.DragOver) == 0 && ac == DragDropAction.Default)
					ac = DragDropAction.None;
			}

			if (ac == DragDropAction.None) {
				return SetDragStatus (drop, x, y, (Gdk.DragAction)0);
			}
			else if (ac == DragDropAction.Default) {
				// Undefined, we need the data
				QueryDragData (drop, true);
				return DragDropInfo.LastStatus;
			}
			else {
				return SetDragStatus (drop, x, y, ConvertDragAction (ac));
			}
		}

		Gdk.DragAction SetDragStatus (Gdk.Drop drop, double x, double y, Gdk.DragAction action)
		{
			DragDropInfo.LastStatus = action;
			OnSetDragStatus (drop, (int)x, (int)y, action);
			return action;
		}

		internal bool DoDragDrop (Gdk.Drop drop, double x, double y)
		{
			DragDropInfo.LastDragPosition = new Point (x, y);
			var cda = ConvertDragAction (DragDropInfo.LastStatus != 0 ? DragDropInfo.LastStatus : drop.Actions);

			DragDropResult res;
			if ((enabledEvents & WidgetEvent.DragDropCheck) == 0) {
				if ((enabledEvents & WidgetEvent.DragDrop) != 0)
					res = DragDropResult.None;
				else
					res = DragDropResult.Canceled;
			}
			else {
				DragCheckEventArgs da = new DragCheckEventArgs (new Point (x, y), Util.GetDragTypes (drop.Formats), cda);
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnDragDropCheck (da);
				});
				res = da.Result;
				if ((enabledEvents & WidgetEvent.DragDrop) == 0 && res == DragDropResult.None)
					res = DragDropResult.Canceled;
			}
			if (res == DragDropResult.Canceled) {
				drop.Finish ((Gdk.DragAction)0);
				return false;
			}
			else if (res == DragDropResult.Success) {
				drop.Finish (ConvertDragAction (cda));
				return true;
			}
			else {
				// Undefined, we need the data
				QueryDragData (drop, false);
				return true;
			}
		}

		void HandleDragLeave (object o, Gtk.DragLeaveArgs args)
		{
			DragDropInfo.LastStatus = 0;
			OnDragLeave ();
			ApplicationContext.InvokeUserCode (delegate {
				eventSink.OnDragLeave (EventArgs.Empty);
			});
		}

		protected virtual void OnDragLeave ()
		{
		}

		void QueryDragData (Gdk.Drop drop, bool isMotionEvent)
		{
			var info = DragDropInfo;
			var store = new TransferDataStore ();
			info.DragData = store;
			int request = ++info.Requests;
			Util.ReadTransferData (ApplicationContext, drop, info.ValidDropTypes, store, delegate {
				if (request != info.Requests || info.DragData != store)
					return; // superseded by a newer request
				OnDragDataReceived (drop, store, isMotionEvent);
			});
		}

		void OnDragDataReceived (Gdk.Drop drop, TransferDataStore data, bool forMotion)
		{
			var pos = DragDropInfo.LastDragPosition;
			if (forMotion) {
				// If no specific action is set, it means that no key has been pressed.
				// In that case, use Move or Copy or Link as default (when allowed, in this order).
				var cact = ConvertDragAction (drop.Actions);
				if (cact != DragDropAction.Copy && cact != DragDropAction.Move && cact != DragDropAction.Link) {
					if (cact.HasFlag (DragDropAction.Move))
						cact = DragDropAction.Move;
					else if (cact.HasFlag (DragDropAction.Copy))
						cact = DragDropAction.Copy;
					else if (cact.HasFlag (DragDropAction.Link))
						cact = DragDropAction.Link;
					else
						cact = DragDropAction.None;
				}

				DragOverEventArgs da = new DragOverEventArgs (pos, data, cact);
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnDragOver (da);
				});
				var action = SetDragStatus (drop, pos.X, pos.Y, ConvertDragAction (da.AllowedAction));
				drop.Status (drop.Actions, action);
			}
			else {
				// Use the action selected during drag over
				var cda = ConvertDragAction (DragDropInfo.LastStatus != 0 ? DragDropInfo.LastStatus : drop.Actions);
				DragEventArgs da = new DragEventArgs (pos, data, cda);
				ApplicationContext.InvokeUserCode (delegate {
					EventSink.OnDragDrop (da);
				});
				drop.Finish (da.Success ? ConvertDragAction (cda) : (Gdk.DragAction)0);
			}
		}

		/// <summary>
		/// Called whenever the drop status changes while dragging over the widget.
		/// </summary>
		protected virtual void OnSetDragStatus (Gdk.Drop drop, int x, int y, Gdk.DragAction action)
		{
		}

		public void DragStart (DragStartData sdata)
		{
			// a drag started programmatically, without a drag gesture
			var native = EventsRootWidget.Native;
			var surface = native?.Surface;
			var device = EventsRootWidget.Display?.DefaultSeat?.Pointer;
			if (surface == null || device == null)
				return;

			Gdk.DragAction action = ConvertDragAction (sdata.DragAction);
			DragDropInfo.CurrentDragData = sdata.Data;
			var content = Util.CreateContentProvider (sdata.Data);
			double x = lastPointerPosition.X, y = lastPointerPosition.Y;
			double nx, ny;
			if (EventsRootWidget.TranslateCoordinates (EventsRootWidget.GetNativeWidget (), x, y, out nx, out ny)) {
				x = nx;
				y = ny;
			}
			var drag = Gdk.Drag.Begin (surface, device, content, action, x, y);
			if (drag == null)
				return;
			Util.CurrentDragSource = sdata.Data;
			if (sdata.ImageBackend != null) {
				var texture = ((GtkImage)sdata.ImageBackend).ToTexture (ApplicationContext, Widget);
				Gtk.DragIcon.SetFromPaintable (drag, texture, (int)sdata.HotX, (int)sdata.HotY);
			}
			drag.DropPerformed += delegate { };
			drag.DndFinished += delegate {
				FinishDrag (drag.SelectedAction == Gdk.DragAction.Move);
			};
			drag.Cancel += delegate {
				FinishDrag (false);
			};
		}

		void HandleDragPrepare (object o, Gtk.PrepareArgs args)
		{
			// If SetDragSource has not been called, ignore the event
			if (DragDropInfo.SourceDragAction == default (Gdk.DragAction))
				return;

			DragStartData sdata = null;
			ApplicationContext.InvokeUserCode (delegate {
				sdata = EventSink.OnDragStarted ();
			});

			if (sdata == null)
				return;

			DragDropInfo.CurrentDragData = sdata.Data;
			Util.CurrentDragSource = sdata.Data;

			if (sdata.ImageBackend != null) {
				var texture = ((GtkImage)sdata.ImageBackend).ToTexture (ApplicationContext, Widget);
				dragSource.SetIcon (texture, (int)sdata.HotX, (int)sdata.HotY);
			}
			args.RetVal = Util.CreateContentProvider (sdata.Data);
		}

		void HandleDragEnd (object o, Gtk.DragEndArgs args)
		{
			FinishDrag (args.DeleteData);
		}

		void FinishDrag (bool delete)
		{
			if (Util.CurrentDragSource == DragDropInfo.CurrentDragData)
				Util.CurrentDragSource = null;
			ApplicationContext.InvokeUserCode (delegate {
				eventSink.OnDragFinished (new DragFinishedEventArgs (delete));
			});
		}

		public void SetDragTarget (TransferDataType[] types, DragDropAction dragAction)
		{
			DragDropInfo.DestDragAction = ConvertDragAction (dragAction);
			DragDropInfo.ValidDropTypes = types;
			OnSetDragTarget (Util.BuildContentFormats (types), DragDropInfo.DestDragAction);
		}

		protected virtual void OnSetDragTarget (Gdk.ContentFormats formats, Gdk.DragAction actions)
		{
			if (dropTarget == null) {
				dropTarget = new Gtk.DropTargetAsync (formats, actions);
				dropTarget.DragEnter += (o, args) => args.RetVal = DoDragMotion (args.Drop, args.X, args.Y);
				dropTarget.DragMotion += (o, args) => args.RetVal = DoDragMotion (args.Drop, args.X, args.Y);
				dropTarget.DragLeave += HandleDragLeave;
				dropTarget.Drop += (o, args) => args.RetVal = DoDragDrop (args.Drop, args.X, args.Y);
				EventsRootWidget.AddController (dropTarget);
			} else {
				dropTarget.Formats = formats;
				dropTarget.Actions = actions;
			}
		}

		public void SetDragSource (TransferDataType[] types, DragDropAction dragAction)
		{
			DragDropInfo.SourceDragAction = ConvertDragAction (dragAction);
			OnSetDragSource (Util.BuildContentFormats (types), DragDropInfo.SourceDragAction);
		}

		protected virtual void OnSetDragSource (Gdk.ContentFormats formats, Gdk.DragAction actions)
		{
			if (dragSource == null) {
				dragSource = new Gtk.DragSource ();
				dragSource.Prepare += HandleDragPrepare;
				dragSource.DragEnd += HandleDragEnd;
				EventsRootWidget.AddController (dragSource);
			}
			dragSource.Actions = actions;
		}

		protected static Gdk.DragAction ConvertDragAction (DragDropAction dragAction)
		{
			Gdk.DragAction action = (Gdk.DragAction)0;
			if ((dragAction & DragDropAction.Copy) != 0)
				action |= Gdk.DragAction.Copy;
			if ((dragAction & DragDropAction.Move) != 0)
				action |= Gdk.DragAction.Move;
			if ((dragAction & DragDropAction.Link) != 0)
				action |= Gdk.DragAction.Link;
			return action;
		}

		protected static DragDropAction ConvertDragAction (Gdk.DragAction dragAction)
		{
			DragDropAction action = (DragDropAction)0;
			if ((dragAction & Gdk.DragAction.Copy) != 0)
				action |= DragDropAction.Copy;
			if ((dragAction & Gdk.DragAction.Move) != 0)
				action |= DragDropAction.Move;
			if ((dragAction & Gdk.DragAction.Link) != 0)
				action |= DragDropAction.Link;
			return action;
		}

		#endregion
	}

	public interface IGtkWidgetBackend
	{
		Gtk.Widget Widget { get; }
	}

	public interface IConstraintProvider
	{
		void GetConstraints (Gtk.Widget target, out SizeConstraint width, out SizeConstraint height);
	}

	/// <summary>
	/// Single-child wrapper that applies the Xwt margins and placement of its child through the
	/// child's GTK margin/align properties; <see cref="Gtk.BinLayout"/> honours them when allocating.
	/// </summary>
	public class WidgetPlacementWrapper: Gtk.Widget, IConstraintProvider
	{
		Gtk.Widget child;

		public WidgetPlacementWrapper ()
		{
			LayoutManager = new Gtk.BinLayout ();
		}

		public Gtk.Widget Child {
			get { return child; }
			set {
				if (child == value)
					return;
				if (child != null) {
					ResetPlacement (child);
					child.Unparent ();
				}
				child = value;
				if (child != null)
					child.Parent = this;
			}
		}

		static void ResetPlacement (Gtk.Widget w)
		{
			w.MarginStart = w.MarginEnd = w.MarginTop = w.MarginBottom = 0;
			w.Halign = Gtk.Align.Fill;
			w.Valign = Gtk.Align.Fill;
		}

		static Gtk.Align ToAlign (WidgetPlacement p)
		{
			switch (p) {
			case WidgetPlacement.Start: return Gtk.Align.Start;
			case WidgetPlacement.Center: return Gtk.Align.Center;
			case WidgetPlacement.End: return Gtk.Align.End;
			default: return Gtk.Align.Fill;
			}
		}

		public void UpdatePlacement (Xwt.Widget widget)
		{
			if (child == null)
				return;
			child.MarginStart = (int)widget.MarginLeft;
			child.MarginEnd = (int)widget.MarginRight;
			child.MarginTop = (int)widget.MarginTop;
			child.MarginBottom = (int)widget.MarginBottom;
			child.Halign = ToAlign (widget.HorizontalPlacement);
			child.Valign = ToAlign (widget.VerticalPlacement);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && child != null) {
				child.Unparent ();
				child = null;
			}
			base.Dispose (disposing);
		}

		public void GetConstraints (Gtk.Widget target, out SizeConstraint width, out SizeConstraint height)
		{
			if (Parent is IConstraintProvider)
				((IConstraintProvider)Parent).GetConstraints (this, out width, out height);
			else
				width = height = SizeConstraint.Unconstrained;
		}
	}
}
