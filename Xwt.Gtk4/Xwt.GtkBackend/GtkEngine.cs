// 
// GtkEngine.cs
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
using Xwt.CairoBackend;
using System.Reflection;
using System.IO;

namespace Xwt.GtkBackend
{
	public class GtkEngine: ToolkitEngineBackend
	{
		GtkPlatformBackend platformBackend;
		static GLib.MainLoop mainLoop;

		public override void InitializeApplication ()
		{
			if (!Gtk.Global.IsInitialized) {
				var args = Environment.GetCommandLineArgs ();
				if (args != null && args.Length > 0)
					GLib.Global.ProgramName = Path.GetFileNameWithoutExtension (args [0]);
				Gtk.Global.Init ();
			}
			if (!(System.Threading.SynchronizationContext.Current is GLib.GLibSynchronizationContext))
				System.Threading.SynchronizationContext.SetSynchronizationContext (new GLib.GLibSynchronizationContext ());
		}

		public override void InitializeBackends ()
		{
			RegisterBackend<ICustomWidgetBackend, CustomWidgetBackend> ();
			RegisterBackend<IWindowBackend, WindowBackend> ();
			RegisterBackend<ILabelBackend, LabelBackend> ();
			RegisterBackend<IBoxBackend, BoxBackend> ();
			RegisterBackend<IButtonBackend, ButtonBackend> ();
			RegisterBackend<INotebookBackend, NotebookBackend> ();
			RegisterBackend<ITreeViewBackend, TreeViewBackend> ();
			RegisterBackend<ITreeStoreBackend, TreeStoreBackend> ();
			RegisterBackend<IListViewBackend, ListViewBackend> ();
			RegisterBackend<IListStoreBackend, ListStoreBackend> ();
			RegisterBackend<ICanvasBackend, CanvasBackend> ();
			RegisterBackend<ImageBackendHandler, ImageHandler> ();
			RegisterBackend<Xwt.Backends.ContextBackendHandler, CairoContextBackendHandler> ();
			RegisterBackend<TextLayoutBackendHandler, GtkTextLayoutBackendHandler> ();
			RegisterBackend<DrawingPathBackendHandler, CairoContextBackendHandler> ();
			RegisterBackend<GradientBackendHandler, CairoGradientBackendHandler> ();
			RegisterBackend<FontBackendHandler, GtkFontBackendHandler> ();
			RegisterBackend<IMenuBackend, MenuBackend> ();
			RegisterBackend<IMenuItemBackend, MenuItemBackend> ();
			RegisterBackend<ICheckBoxMenuItemBackend, CheckBoxMenuItemBackend> ();
			RegisterBackend<IRadioButtonMenuItemBackend, RadioButtonMenuItemBackend> ();
			RegisterBackend<ISeparatorMenuItemBackend, SeparatorMenuItemBackend> ();
			RegisterBackend<IScrollViewBackend, ScrollViewBackend> ();
			RegisterBackend<IComboBoxBackend, ComboBoxBackend> ();
			RegisterBackend<IDesignerSurfaceBackend, DesignerSurfaceBackend> ();
			RegisterBackend<IMenuButtonBackend, MenuButtonBackend> ();
			RegisterBackend<ITextEntryBackend, TextEntryBackend> ();
			RegisterBackend<IToggleButtonBackend, ToggleButtonBackend> ();
			RegisterBackend<IImageViewBackend, ImageViewBackend> ();
			RegisterBackend<IAlertDialogBackend, AlertDialogBackend> ();
			RegisterBackend<ICheckBoxBackend, CheckBoxBackend> ();
			RegisterBackend<IFrameBackend, FrameBackend> ();
			RegisterBackend<ISeparatorBackend, SeparatorBackend> ();
			RegisterBackend<IDialogBackend, DialogBackend> ();
			RegisterBackend<IComboBoxEntryBackend, ComboBoxEntryBackend> ();
			RegisterBackend<ClipboardBackend, GtkClipboardBackend> ();
			RegisterBackend<ImagePatternBackendHandler, GtkImagePatternBackendHandler> ();
			RegisterBackend<ImageBuilderBackendHandler, ImageBuilderBackend> ();
			RegisterBackend<IScrollAdjustmentBackend, ScrollAdjustmentBackend> ();
			RegisterBackend<IOpenFileDialogBackend, OpenFileDialogBackend> ();
			RegisterBackend<ISaveFileDialogBackend, SaveFileDialogBackend> ();
			RegisterBackend<ISelectFolderDialogBackend, SelectFolderDialogBackend> ();
			RegisterBackend<IPanedBackend, PanedBackend> ();
			RegisterBackend<ISelectColorDialogBackend, SelectColorDialogBackend> ();
			RegisterBackend<IListBoxBackend, ListBoxBackend> ();
			RegisterBackend<IStatusIconBackend, StatusIconBackend> ();
			RegisterBackend<IProgressBarBackend, ProgressBarBackend> ();
			RegisterBackend<IPopoverBackend, PopoverBackend> ();
			RegisterBackend<ISpinButtonBackend, SpinButtonBackend> ();
			RegisterBackend<IDatePickerBackend, DatePickerBackend> ();
			RegisterBackend<ILinkLabelBackend, LinkLabelBackend> ();
			RegisterBackend<ISpinnerBackend, SpinnerBackend> ();
			RegisterBackend<IRichTextViewBackend, RichTextViewBackend> ();
			RegisterBackend<IExpanderBackend, ExpanderBackend> ();
			RegisterBackend<DesktopBackend, GtkDesktopBackend> ();
			RegisterBackend<IEmbeddedWidgetBackend, EmbeddedWidgetBackend> ();
			RegisterBackend<ISegmentedButtonBackend, SegmentedButtonBackend> ();
			RegisterBackend<ISliderBackend, SliderBackend> ();
			RegisterBackend<IRadioButtonBackend, RadioButtonBackend> ();
			RegisterBackend<IScrollbarBackend, ScrollbarBackend> ();
			RegisterBackend<IPasswordEntryBackend, PasswordEntryBackend> ();
			RegisterBackend<KeyboardHandler, GtkKeyboardHandler> ();
			RegisterBackend<ISearchTextEntryBackend, SearchTextEntryBackend> ();
			RegisterBackend<IWebViewBackend, WebViewBackend> ();
			RegisterBackend<IColorSelectorBackend, ColorSelectorBackend> ();
			RegisterBackend<IColorPickerBackend, ColorPickerBackend> ();
			RegisterBackend<ICalendarBackend, CalendarBackend> ();
			RegisterBackend<IFontSelectorBackend, FontSelectorBackend> ();
			RegisterBackend<ISelectFontDialogBackend, SelectFontDialogBackend> ();
			RegisterBackend<IAccessibleBackend, AccessibleBackend> ();
			RegisterBackend<IPopupWindowBackend, PopupWindowBackend> ();
			RegisterBackend<IUtilityWindowBackend, UtilityWindowBackend> ();


			string typeName = null;
			string asmName = null;
			if (Platform.IsWindows) {
				typeName = "Xwt.Gtk.Windows.WindowsPlatformBackend";
				asmName = "Xwt.Gtk4.Windows";
			}

			if (typeName != null) {
				var loc = Path.GetDirectoryName (GetType ().Assembly.Location);
				loc = Path.Combine (loc, asmName + ".dll");

				Assembly asm = null;
				try {
					if (File.Exists (loc)) {
						asm = Assembly.LoadFrom (loc);
					} else {
						asm = Assembly.Load (asmName);
					}
				} catch {
					// Not found
				}

				Type platformType = asm != null ? asm.GetType (typeName) : null;

				if (platformType != null) {
					platformBackend = (GtkPlatformBackend)Activator.CreateInstance (platformType);
					platformBackend.Initialize (this);
				}
			}
		}

		public override void Dispose ()
		{
			GtkTextLayoutBackendHandler.DisposeResources ();
			base.Dispose();
		}

		/// <summary>
		/// The main loop driven by <see cref="RunApplication"/> (GTK 4 has no gtk_main).
		/// </summary>
		public static GLib.MainLoop MainLoop {
			get {
				if (mainLoop == null)
					mainLoop = new GLib.MainLoop ();
				return mainLoop;
			}
		}

		public override void RunApplication ()
		{
			MainLoop.Run ();
		}

		public override void ExitApplication ()
		{
			if (mainLoop != null && mainLoop.IsRunning)
				mainLoop.Quit ();
		}

		public override bool HandlesSizeNegotiation {
			get {
				return true;
			}
		}

		/// <summary>
		/// Puts <paramref name="newWidget"/> in place of <paramref name="oldWidget"/> in its parent.
		/// </summary>
		public static void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			var parent = oldWidget.Parent;
			if (parent == null)
				return;

			if (parent is IGtkContainer) {
				((IGtkContainer)parent).ReplaceChild (oldWidget, newWidget);
			}
			else if (parent is WidgetPlacementWrapper) {
				((WidgetPlacementWrapper)parent).Child = newWidget;
			}
			else if (parent is Gtk.Box) {
				var box = (Gtk.Box)parent;
				var prev = oldWidget.PrevSibling;
				box.Remove (oldWidget);
				box.InsertChildAfter (newWidget, prev);
			}
			else if (parent is Gtk.Paned) {
				var paned = (Gtk.Paned)parent;
				var pos = paned.Position;
				if (paned.StartChild == oldWidget)
					paned.StartChild = newWidget;
				else
					paned.EndChild = newWidget;
				paned.Position = pos;
			}
			else if (IsNotebookPage (oldWidget)) {
				// notebook pages live in an internal stack of the notebook
				ReplaceNotebookPage (FindAncestor<Gtk.Notebook> (parent), oldWidget, newWidget);
			}
			else {
				// Window, Frame, Expander, ScrolledWindow, Viewport, Overlay, Popover, Button...
				// all expose their single child through the "child" property
				SetChildProperty (parent, newWidget, oldWidget);
			}
		}

		static bool IsNotebookPage (Gtk.Widget w)
		{
			var nb = FindAncestor<Gtk.Notebook> (w.Parent);
			return nb != null && nb.PageNum (w) >= 0;
		}

		static T FindAncestor<T> (Gtk.Widget w) where T: Gtk.Widget
		{
			while (w != null && !(w is T))
				w = w.Parent;
			return (T)w;
		}

		static void ReplaceNotebookPage (Gtk.Notebook notebook, Gtk.Widget oldWidget, Gtk.Widget newWidget)
		{
			if (notebook == null)
				return;
			int pos = notebook.PageNum (oldWidget);
			if (pos < 0)
				return;
			var label = notebook.GetTabLabel (oldWidget);
			var reorderable = notebook.GetTabReorderable (oldWidget);
			var detachable = notebook.GetTabDetachable (oldWidget);
			if (label != null)
				notebook.SetTabLabel (oldWidget, null);
			notebook.RemovePage (pos);
			notebook.InsertPage (newWidget, label, pos);
			notebook.SetTabReorderable (newWidget, reorderable);
			notebook.SetTabDetachable (newWidget, detachable);
		}

		static void SetChildProperty (Gtk.Widget parent, Gtk.Widget child, Gtk.Widget expected)
		{
			using (var current = parent.GetProperty ("child")) {
				if (expected != null && (current.Val as Gtk.Widget) != expected)
					return;
			}
			using (var v = new GLib.Value (child))
				parent.SetProperty ("child", v);
		}

		/// <summary>
		/// Removes a widget from its parent, whatever the parent type.
		/// </summary>
		public static void RemoveChild (Gtk.Widget child)
		{
			var parent = child.Parent;
			if (parent == null)
				return;
			if (parent is IGtkContainer)
				((IGtkContainer)parent).RemoveChild (child);
			else if (parent is WidgetPlacementWrapper)
				((WidgetPlacementWrapper)parent).Child = null;
			else if (parent is Gtk.Box)
				((Gtk.Box)parent).Remove (child);
			else if (parent is Gtk.Paned) {
				var paned = (Gtk.Paned)parent;
				if (paned.StartChild == child)
					paned.StartChild = null;
				else
					paned.EndChild = null;
			}
			else if (IsNotebookPage (child)) {
				var notebook = FindAncestor<Gtk.Notebook> (parent);
				notebook.RemovePage (notebook.PageNum (child));
			}
			else {
				try {
					SetChildPropertyNull (parent, child);
				} catch {
					child.Unparent ();
				}
			}
		}

		static void SetChildPropertyNull (Gtk.Widget parent, Gtk.Widget child)
		{
			using (var current = parent.GetProperty ("child")) {
				if ((current.Val as Gtk.Widget) != child) {
					child.Unparent ();
					return;
				}
			}
			using (var v = new GLib.Value (Gtk.Widget.GType))
				parent.SetProperty ("child", v);
		}

		public override void InvokeAsync (Action action)
		{
			if (action == null)
				throw new ArgumentNullException ("action");

			GLib.Idle.Add (GLib.Priority.Default, () => {
				action ();
				return false;
			});
		}

		public override object TimerInvoke (Func<bool> action, TimeSpan timeSpan)
		{
			if (action == null)
				throw new ArgumentNullException ("action");
			if (timeSpan.TotalMilliseconds < 0)
				throw new ArgumentException ("Timer period must be >=0", "timeSpan");

			return GLib.Timeout.Add ((uint) timeSpan.TotalMilliseconds, action.Invoke);
		}

		public override void CancelTimerInvoke (object id)
		{
			if (id == null)
				throw new ArgumentNullException ("id");

			GLib.Source.Remove ((uint)id);
		}

		/// <summary>Returns the <see cref="Gtk.Widget"/> of the Xwt widget.</summary>
		public override object GetNativeWidget (Widget w)
		{
			IGtkWidgetBackend wb = (IGtkWidgetBackend)Toolkit.GetBackend (w);
			return wb.Widget;
		}

		/// <summary>Returns a <see cref="Gdk.Pixbuf"/> rendering of the image at its size.</summary>
		public override object GetNativeImage (Xwt.Drawing.Image image)
		{
			var pix = (GtkImage)Toolkit.GetBackend (image);
			return pix.ToPixbuf (ApplicationContext, image.Size.Width, image.Size.Height);
		}

		public override IWindowFrameBackend GetBackendForWindow (object nativeWindow)
		{
			var win = new WindowFrameBackend ();
			win.Window = (Gtk.Window) nativeWindow;
			return win;
		}

		public override object GetNativeWindow (IWindowFrameBackend backend)
		{
			return backend.Window as Gtk.Window;
		}

		public override object GetBackendForImage (object nativeImage)
		{
			if (nativeImage is Gdk.Pixbuf)
				return new GtkImage ((Gdk.Pixbuf)nativeImage);
			else if (nativeImage is Gdk.Texture)
				return new GtkImage ((Gdk.Texture)nativeImage);
			else if (nativeImage is string)
				return new GtkImage ((string)nativeImage);
			else if (nativeImage is GtkImage)
				return nativeImage;
			else
				throw new NotSupportedException ();
		}

		public override object GetBackendForContext (object nativeWidget, object nativeContext)
		{
			Gtk.Widget w = (Gtk.Widget)nativeWidget;
			return new CairoContextBackend (Util.GetScaleFactor (w)) { Context = (Cairo.Context)nativeContext };
		}

		public override object GetNativeParentWindow (Widget w)
		{
			IGtkWidgetBackend wb = (IGtkWidgetBackend)Toolkit.GetBackend (w);
			return wb.Widget.Root as Gtk.Window;
		}

		public override bool HasNativeParent (Widget w)
		{
			IGtkWidgetBackend wb = (IGtkWidgetBackend)Toolkit.GetBackend (w);
			return wb.Widget.Parent != null;
		}

		public override void DispatchPendingEvents ()
		{
			// The loop is limited to 1000 iterations as a workaround for an issue that some users
			// have experienced. Sometimes Pending starts return 'true' for all iterations,
			// causing the loop to never end.
			int n = 1000;
			var context = GLib.MainContext.Default;
			while (GLib.MainContext.Pending () && --n > 0)
				context.RunIteration (false);
		}

		public override object RenderWidget (Widget widget)
		{
			var w = ((WidgetBackend)widget.GetBackend ()).Widget;
			int width = w.Width, height = w.Height;
			if (!w.IsMapped || width <= 0 || height <= 0)
				throw new InvalidOperationException ();

			var scale = Util.GetScaleFactor (w);
			var paintable = new Gtk.WidgetPaintable (w);
			var snapshot = new Gtk.Snapshot ();
			paintable.Snapshot (snapshot, width, height);
			var node = snapshot.FreeToNode ();
			using (var surface = new Cairo.ImageSurface (Cairo.Format.Argb32, (int)(width * scale), (int)(height * scale))) {
				if (node != null) {
					using (var cr = new Cairo.Context (surface)) {
						cr.Scale (scale, scale);
						node.Draw (cr);
					}
				}
				var frame = new GtkImage.ImageFrame (surface.ToPixbuf (), width, height, true);
				return new GtkImage (new [] { frame });
			}
		}

		public override void RenderImage (object nativeWidget, object nativeContext, ImageDescription img, double x, double y)
		{
			GtkImage gim = (GtkImage)img.Backend;
			Cairo.Context ctx = nativeContext as Cairo.Context;
			Gtk.Widget w = (Gtk.Widget)nativeWidget;
			if (ctx != null)
				gim.Draw (ApplicationContext, ctx, Util.GetScaleFactor (w), x, y, img);
		}

		public override Rectangle GetScreenBounds (object nativeWidget)
		{
			var widget = nativeWidget as Gtk.Widget;
			if (widget == null)
				throw new InvalidOperationException ("Widget belongs to a different toolkit");

			var origin = GtkWorkarounds.ConvertToScreenCoordinates (widget, Point.Zero);
			return new Rectangle (origin.X, origin.Y, widget.Width, widget.Height);
		}

		public override ToolkitFeatures SupportedFeatures {
			get {
				var f = ToolkitFeatures.All;
				// GTK 4 has no window opacity on Windows and no client-side window positioning
				if (Platform.IsWindows)
					f &= ~ToolkitFeatures.WindowOpacity;
				return f;
			}
		}

		protected override Type GetBackendImplementationType (Type backendType)
		{
			if (platformBackend != null) {
				var bt = platformBackend.GetBackendImplementationType (backendType);
				if (bt != null)
					return bt;
			}
			return base.GetBackendImplementationType (backendType);
		}
	}

	/// <summary>
	/// Implemented by backend-owned container widgets so children can be swapped/removed generically.
	/// </summary>
	public interface IGtkContainer
	{
		void ReplaceChild (Gtk.Widget oldWidget, Gtk.Widget newWidget);
		void RemoveChild (Gtk.Widget child);
	}
}
