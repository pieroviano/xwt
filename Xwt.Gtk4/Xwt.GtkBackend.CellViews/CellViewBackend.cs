//
// CellRendererBackend.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2014 Xamarin, Inc (http://www.xamarin.com)
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
using Gtk;
using TreeModel = Gtk.ITreeModel;

namespace Xwt.GtkBackend
{
	public class CellViewBackend: ICellViewBackend, ICellDataSource
	{
		ICellRendererTarget rendererTarget;
		object target;
		bool buttonPressSubscribed;
		bool buttonReleaseSubscribed;
		WidgetEvent enabledEvents;
		bool enabledMouseEvents;
		bool mouseInsideCell;
		Gtk.TreeIter mouseIter;

		// GTK 4: button events are read through a legacy controller, not a GestureClick: the tree
		// view claims the press with its own gesture, which would deny a gesture of ours and drop
		// the release. Running in the capture phase reproduces the GTK 2/3 "connect before" handlers.
		Gtk.EventControllerLegacy buttonController;
		Gtk.EventControllerMotion motionController;
		Gtk.EventControllerKey keyController;

		public CellViewBackend ()
		{
		}

		public virtual void Initialize (ICellViewFrontend cellView, ICellRendererTarget rendererTarget, object target)
		{
			Frontend = cellView;
			this.rendererTarget = rendererTarget;
			this.target = target;
		}

		protected ICellRendererTarget CellRendererTarget {
			get { return rendererTarget; }
		}

		public ApplicationContext ApplicationContext { get; private set; }

		public ICellViewFrontend Frontend { get; private set; }

		public Gtk.CellRenderer CellRenderer {
			get;
			protected set;
		}

		public TreeModel TreeModel { get; private set; }

		public Gtk.TreeIter CurrentIter { get; private set; }

		public ICellViewEventSink EventSink { get; private set; }

		public void InitializeBackend (object frontend, ApplicationContext context)
		{
			ApplicationContext = context;
		}

		public virtual void EnableEvent (object eventId)
		{
			if (eventId is WidgetEvent) {
				WidgetEvent ev = (WidgetEvent) eventId;
				enabledEvents |= ev;
				switch (ev) {
				case WidgetEvent.MouseMoved:
				case WidgetEvent.MouseEntered:
				case WidgetEvent.MouseExited:
					EnableMouseEvents ();
					break;
				case WidgetEvent.ButtonPressed:
					buttonPressSubscribed = true;
					EnableButtonEvents ();
					break;
				case WidgetEvent.ButtonReleased:
					buttonReleaseSubscribed = true;
					EnableButtonEvents ();
					break;
				case WidgetEvent.KeyPressed:
				case WidgetEvent.KeyReleased:
					EnableMouseEvents ();
					EnableKeyEvents ();
					break;
				}
			}
		}

		public virtual void DisableEvent (object eventId)
		{
			if (eventId is WidgetEvent) {
				WidgetEvent ev = (WidgetEvent) eventId;
				enabledEvents &= ~ev;
				switch (ev) {
				case WidgetEvent.MouseMoved:
				case WidgetEvent.MouseEntered:
				case WidgetEvent.MouseExited:
					DisableMouseEvents ();
					break;
				case WidgetEvent.ButtonPressed:
					buttonPressSubscribed = false;
					DisableButtonEvents ();
					break;
				case WidgetEvent.ButtonReleased:
					buttonReleaseSubscribed = false;
					DisableButtonEvents ();
					break;
				case WidgetEvent.KeyPressed:
				case WidgetEvent.KeyReleased:
					DisableMouseEvents ();
					DisableKeyEvents ();
					break;
				}
			}
		}

		void EnableButtonEvents ()
		{
			if (buttonController == null) {
				buttonController = new Gtk.EventControllerLegacy ();
				buttonController.PropagationPhase = Gtk.PropagationPhase.Capture;
				LegacyEvents.Connect (buttonController, HandleLegacyEvent);
				rendererTarget.EventRootWidget.AddController (buttonController);
			}
		}

		void DisableButtonEvents ()
		{
			if (buttonController != null && !buttonPressSubscribed && !buttonReleaseSubscribed) {
				LegacyEvents.Disconnect (buttonController, HandleLegacyEvent);
				rendererTarget.EventRootWidget.RemoveController (buttonController);
				buttonController = null;
			}
		}

		void EnableKeyEvents ()
		{
			if (keyController == null) {
				keyController = new Gtk.EventControllerKey ();
				keyController.PropagationPhase = Gtk.PropagationPhase.Capture;
				keyController.KeyPressed += HandleKeyPressed;
				keyController.KeyReleased += HandleKeyReleased;
				rendererTarget.EventRootWidget.AddController (keyController);
			}
		}

		void DisableKeyEvents ()
		{
			if (keyController != null && (enabledEvents & (WidgetEvent.KeyPressed | WidgetEvent.KeyReleased)) == 0) {
				keyController.KeyPressed -= HandleKeyPressed;
				keyController.KeyReleased -= HandleKeyReleased;
				rendererTarget.EventRootWidget.RemoveController (keyController);
				keyController = null;
			}
		}

		void EnableMouseEvents ()
		{
			if (!enabledMouseEvents) {
				motionController = new Gtk.EventControllerMotion ();
				motionController.Motion += HandleMotion;
				motionController.Leave += HandleLeave;
				rendererTarget.EventRootWidget.AddController (motionController);
				enabledMouseEvents = true;
			}
		}

		void DisableMouseEvents ()
		{
			if ((enabledEvents & (WidgetEvent.MouseMoved | WidgetEvent.MouseEntered | WidgetEvent.MouseExited | WidgetEvent.KeyPressed | WidgetEvent.KeyReleased)) != 0)
				return;
			if (enabledMouseEvents) {
				motionController.Motion -= HandleMotion;
				motionController.Leave -= HandleLeave;
				rendererTarget.EventRootWidget.RemoveController (motionController);
				motionController = null;
				enabledMouseEvents = false;
				mouseInsideCell = false;
			}
		}

		/// <summary>Position of a pointer event in EventRootWidget coordinates.</summary>
		bool GetEventPosition (Gdk.Event evt, out double x, out double y)
		{
			x = y = 0;
			double ex, ey, sx, sy;
			var widget = rendererTarget.EventRootWidget;
			var native = widget.Native;
			var nativeWidget = widget.GetNativeWidget ();
			if (native == null || nativeWidget == null || !evt.GetPosition (out ex, out ey))
				return false;
			// event coordinates are relative to the surface, which is offset from the native widget
			native.GetSurfaceTransform (out sx, out sy);
			return nativeWidget.TranslateCoordinates (widget, ex - sx, ey - sy, out x, out y);
		}

		bool HandleLegacyEvent (Gdk.Event evt)
		{
			var type = evt.EventType;
			if (type != Gdk.EventType.ButtonPress && type != Gdk.EventType.ButtonRelease)
				return false;
			double x, y;
			if (!GetEventPosition (evt, out x, out y))
				return false;
			var button = new Gdk.ButtonEvent (evt.Handle).Button;
			return type == Gdk.EventType.ButtonPress
				? HandleButtonPress (evt, button, x, y)
				: HandleButtonRelease (button, x, y);
		}

		int pressX, pressY, pressCX, pressCY;

		bool HandleButtonRelease (uint button, double x, double y)
		{
			int cx, cy;
			Gtk.TreeIter iter;
			bool captured = false;

			if (rendererTarget.PressedCell != null) {
				if (rendererTarget.PressedCell == this) {
					iter = rendererTarget.PressedIter;
					cx = pressCX + (int)x - pressX;
					cy = pressCY + (int)y - pressY;
					captured = true;
				}
				else
					return false;
			} else if (!rendererTarget.GetCellPosition (CellRenderer, (int)x, (int)y, out cx, out cy, out iter))
				return false;

			rendererTarget.PressedCell = null;
			rendererTarget.PressedIter = Gtk.TreeIter.Zero;

			if (!buttonReleaseSubscribed)
				return false;

			bool handled = false;
			var rect = rendererTarget.GetCellBackgroundBounds (target, CellRenderer, iter);
			if (captured || rect.Contains (cx, cy)) {
				ApplicationContext.InvokeUserCode (delegate {
					LoadData (rendererTarget.Model, iter);
					SetCurrentEventRow ();
					var a = new ButtonEventArgs {
						X = x,
						Y = y,
						Button = (PointerButton) button
					};
					EventSink.OnButtonReleased (a);
					handled = a.Handled;
				});
			}
			return handled;
		}

		bool HandleButtonPress (Gdk.Event evt, uint button, double x, double y)
		{
			if (!buttonPressSubscribed)
				return false;
			int cx, cy;
			Gtk.TreeIter iter;
			bool handled = false;
			if (rendererTarget.GetCellPosition (CellRenderer, (int)x, (int)y, out cx, out cy, out iter)) {
				var rect = rendererTarget.GetCellBackgroundBounds (target, CellRenderer, iter);
				if (rect.Contains (cx, cy)) {
					rendererTarget.PressedIter = iter;
					rendererTarget.PressedCell = this;
					pressX = (int)x;
					pressY = (int)y;
					pressCX = cx;
					pressCY = cy;
					bool contextMenu = evt.TriggersContextMenu ();
					ApplicationContext.InvokeUserCode (delegate {
						LoadData (rendererTarget.Model, iter);
						SetCurrentEventRow ();
						var a = new ButtonEventArgs {
							X = x,
							Y = y,
							Button = (PointerButton) button,
							IsContextMenuTrigger = contextMenu
						};
						EventSink.OnButtonPressed (a);
						handled = a.Handled;
					});
				}
			}
			return handled;
		}

		void HandleMotion (object o, Gtk.MotionArgs args)
		{
			int cx, cy;
			Gtk.TreeIter iter;
			long time = (long)motionController.CurrentEventTime;
			if (rendererTarget.GetCellPosition (CellRenderer, (int)args.X, (int)args.Y, out cx, out cy, out iter)) {
				var rect = rendererTarget.GetCellBackgroundBounds (target, CellRenderer, iter);

				if (rect.Contains (cx, cy)) {
					if (enabledEvents.HasFlag (WidgetEvent.MouseMoved))
						ApplicationContext.InvokeUserCode (delegate {
							LoadData (rendererTarget.Model, iter);
							SetCurrentEventRow ();
							EventSink.OnMouseMoved (new MouseMovedEventArgs (time, cx, cy));
						});

					mouseIter = iter;
					if (!mouseInsideCell) {
						mouseInsideCell = true;
						if (enabledEvents.HasFlag (WidgetEvent.MouseEntered))
							ApplicationContext.InvokeUserCode (delegate {
								LoadData (rendererTarget.Model, iter);
								SetCurrentEventRow ();
								EventSink.OnMouseEntered ();
							});
					}
					return;
				}
			}
			RaiseMouseExited ();
		}

		void HandleLeave (object o, System.EventArgs args)
		{
			RaiseMouseExited ();
		}

		void RaiseMouseExited ()
		{
			if (!mouseInsideCell)
				return;
			mouseInsideCell = false;
			var iter = mouseIter;
			mouseIter = Gtk.TreeIter.Zero;
			if (enabledEvents.HasFlag (WidgetEvent.MouseExited))
				ApplicationContext.InvokeUserCode (delegate {
					LoadData (rendererTarget.Model, iter);
					SetCurrentEventRow ();
					EventSink.OnMouseExited ();
				});
		}

		void HandleKeyPressed (object o, Gtk.KeyPressedArgs args)
		{
			if (!mouseInsideCell || !enabledEvents.HasFlag (WidgetEvent.KeyPressed))
				return;

			Key k = (Key)args.Keyval;
			ModifierKeys m = args.State.ToXwtValue ();

			KeyEventArgs kargs = new KeyEventArgs (k, (int)args.Keyval, m, false, (long)keyController.CurrentEventTime);
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnKeyPressed (kargs);
			});
		}

		void HandleKeyReleased (object o, Gtk.KeyReleasedArgs args)
		{
			if (!mouseInsideCell || !enabledEvents.HasFlag (WidgetEvent.KeyReleased))
				return;

			Key k = (Key)args.Keyval;
			ModifierKeys m = args.State.ToXwtValue ();

			KeyEventArgs kargs = new KeyEventArgs (k, (int)args.Keyval, m, false, (long)keyController.CurrentEventTime);
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.OnKeyReleased (kargs);
			});
		}

		public void LoadData (TreeModel model, Gtk.TreeIter iter)
		{
			TreeModel = model;
			CurrentIter = iter;
			EventSink = Frontend.Load (this);
			CellRenderer.Visible = Frontend.Visible;

			if (CellRenderer is GtkCellRendererCustom && Frontend.AccessibleFields.Label != null) {
				var label = GetValue (Frontend.AccessibleFields.Label) as string;
				((GtkCellRendererCustom)CellRenderer).AccessibleText = label ?? string.Empty;
			}

			OnLoadData ();
		}

		protected virtual void OnLoadData ()
		{
		}

		public object GetValue (IDataField field)
		{
			return CellUtil.GetModelValue (TreeModel, CurrentIter, field.Index);
		}

		public virtual Rectangle CellBounds {
			get {
				return rendererTarget.GetCellBounds (target, CellRenderer, CurrentIter);
			}
		}

		public virtual Rectangle BackgroundBounds {
			get {
				return rendererTarget.GetCellBackgroundBounds (target, CellRenderer, CurrentIter);
			}
		}

		public virtual bool Selected {
			get {
				return false;
			}
		}

		public virtual bool HasFocus {
			get {
				return false;
			}
		}

		public void SetCurrentEventRow ()
		{
			var path = TreeModel.GetPath (CurrentIter);
			rendererTarget.SetCurrentEventRow (path.ToString ());
		}

		public void QueueDraw ()
		{
			rendererTarget.QueueDraw (target, CurrentIter);
		}

		public void QueueResize ()
		{
			rendererTarget.QueueResize (target, CurrentIter);
		}
	}
}

