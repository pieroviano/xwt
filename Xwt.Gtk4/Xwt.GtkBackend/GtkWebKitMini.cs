//
// GtkWebKitMini.cs
//
// Author:
//       Vsevolod Kukol <sevo@sevo.org>
//
// Copyright (c) 2014 Vsevolod Kukol
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
using System.Runtime.InteropServices;

namespace Xwt.GtkBackend.WebKit
{
	/// <summary>
	/// Minimal binding of WebKitGTK 6.0 (the GTK 4 WebKit) WebKitWebView. The library is resolved at
	/// run time through <see cref="GtkInterop.LoadFunction{T}"/>; check <see cref="IsAvailable"/> before
	/// creating a view.
	/// </summary>
	public class WebView : Gtk.Widget
	{
		public WebView (IntPtr raw) : base (raw)
		{
		}

		public WebView () : base (IntPtr.Zero)
		{
			if (!IsAvailable)
				throw new NotSupportedException ("WebKitGTK 6.0 is not available");
			Initialize ();
			Raw = webkit_web_view_new ();
			ConnectSignals ();
		}

		/// <summary>True when the WebKitGTK 6.0 runtime library and its core entry points are present.</summary>
		public static bool IsAvailable {
			get {
				return webkit_web_view_new != null && webkit_web_view_get_type != null;
			}
		}

		public void LoadUri (string uri)
		{
			IntPtr native_uri = GLib.Marshaller.StringToPtrGStrdup (uri ?? "about:blank");
			webkit_web_view_load_uri (Handle, native_uri);
			GLib.Marshaller.Free (native_uri);
		}

		public string Uri {
			get {
				return GLib.Marshaller.Utf8PtrToString (webkit_web_view_get_uri (Handle));
			}
		}

		public double LoadProgress {
			get {
				return webkit_web_view_get_estimated_load_progress (Handle);
			}
		}

		/// <summary>Zoom the whole page (true) or only its text (false): WebKitSettings "zoom-text-only".</summary>
		public bool FullContentZoom {
			get {
				var settings = WebSettings;
				if (settings == null)
					return true;
				using (var v = settings.GetProperty ("zoom-text-only"))
					return !(bool)v.Val;
			}
			set {
				var settings = WebSettings;
				if (settings == null)
					return;
				using (var v = new GLib.Value (!value))
					settings.SetProperty ("zoom-text-only", v);
			}
		}

		GLib.Object WebSettings {
			get {
				var raw = webkit_web_view_get_settings != null ? webkit_web_view_get_settings (Handle) : IntPtr.Zero;
				return raw == IntPtr.Zero ? null : GLib.Object.GetObject (raw);
			}
		}

		/// <summary>
		/// GTK 4: WebKitGTK 6 has no "transparent" property; a transparent background color
		/// (webkit_web_view_set_background_color) gives the same result.
		/// </summary>
		public bool Transparent {
			get {
				if (webkit_web_view_get_background_color == null)
					return false;
				var rgba = new Gdk.RGBA ();
				webkit_web_view_get_background_color (Handle, ref rgba);
				return rgba.Alpha < 1;
			}
			set {
				if (webkit_web_view_set_background_color == null)
					return;
				var rgba = new Gdk.RGBA { Red = 1, Green = 1, Blue = 1, Alpha = value ? 0 : 1 };
				webkit_web_view_set_background_color (Handle, ref rgba);
			}
		}

		public void StopLoading ()
		{
			webkit_web_view_stop_loading (Handle);
		}

		public void Reload ()
		{
			webkit_web_view_reload (Handle);
		}

		public bool CanGoBack ()
		{
			return webkit_web_view_can_go_back (Handle);
		}

		public void GoBack ()
		{
			webkit_web_view_go_back (Handle);
		}

		public void GoForward ()
		{
			webkit_web_view_go_forward (Handle);
		}

		public bool CanGoForward ()
		{
			return webkit_web_view_can_go_forward (Handle);
		}

		public void LoadHtmlString (string content, string base_uri)
		{
			IntPtr native_content = GLib.Marshaller.StringToPtrGStrdup (content ?? "");
			IntPtr native_base_uri = GLib.Marshaller.StringToPtrGStrdup (base_uri);
			webkit_web_view_load_html (Handle, native_content, native_base_uri);
			GLib.Marshaller.Free (native_content);
			GLib.Marshaller.Free (native_base_uri);
		}

		public string Title {
			get {
				return GLib.Marshaller.Utf8PtrToString (webkit_web_view_get_title (Handle));
			}
		}

		/// <summary>Raised on "load-changed" with WEBKIT_LOAD_FINISHED.</summary>
		public event EventHandler LoadFinished;

		/// <summary>Raised on "load-changed" with WEBKIT_LOAD_STARTED.</summary>
		public event EventHandler LoadStarted;

		/// <summary>Raised on "decide-policy" for navigation and new-window actions.</summary>
		public event EventHandler<NavigationRequestedArgs> NavigationRequested;

		event EventHandler<TitleChangedArgs> titleChanged;

		/// <summary>Raised on "notify::title".</summary>
		public event EventHandler<TitleChangedArgs> TitleChanged {
			add {
				if (titleChanged == null)
					AddNotification ("title", HandleTitleNotify);
				titleChanged += value;
			}
			remove {
				titleChanged -= value;
				if (titleChanged == null)
					RemoveNotification ("title", HandleTitleNotify);
			}
		}

		/// <summary>Raised on "context-menu"; set <see cref="ContextMenuArgs.RetVal"/> to true to suppress the menu.</summary>
		public event EventHandler<ContextMenuArgs> ContextMenu;

		void HandleTitleNotify (object o, GLib.NotifyArgs args)
		{
			titleChanged?.Invoke (this, new TitleChangedArgs (Title));
		}

		// WebKitLoadEvent
		const int WEBKIT_LOAD_STARTED = 0;
		const int WEBKIT_LOAD_FINISHED = 3;
		// WebKitPolicyDecisionType
		const int WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION = 0;
		const int WEBKIT_POLICY_DECISION_TYPE_NEW_WINDOW_ACTION = 1;

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate void LoadChangedCallback (IntPtr view, int loadEvent, IntPtr data);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool DecidePolicyCallback (IntPtr view, IntPtr decision, int decisionType, IntPtr data);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool ContextMenuCallback (IntPtr view, IntPtr menu, IntPtr hitTestResult, IntPtr data);

		static readonly LoadChangedCallback loadChangedCallback = OnLoadChanged;
		static readonly DecidePolicyCallback decidePolicyCallback = OnDecidePolicy;
		static readonly ContextMenuCallback contextMenuCallback = OnContextMenu;

		void ConnectSignals ()
		{
			// the signal args carry WebKit-specific enums/boxed types that GtkSharp cannot marshal,
			// so the signals are connected natively and forwarded to the managed events
			Connect ("load-changed", loadChangedCallback);
			Connect ("decide-policy", decidePolicyCallback);
			Connect ("context-menu", contextMenuCallback);
		}

		void Connect (string signal, Delegate callback)
		{
			if (g_signal_connect_data == null)
				return;
			IntPtr native_signal = GLib.Marshaller.StringToPtrGStrdup (signal);
			g_signal_connect_data (Handle, native_signal, Marshal.GetFunctionPointerForDelegate (callback), IntPtr.Zero, IntPtr.Zero, 0);
			GLib.Marshaller.Free (native_signal);
		}

		static void OnLoadChanged (IntPtr view, int loadEvent, IntPtr data)
		{
			try {
				var w = GLib.Object.TryGetObject (view) as WebView;
				if (w == null)
					return;
				if (loadEvent == WEBKIT_LOAD_STARTED)
					w.LoadStarted?.Invoke (w, EventArgs.Empty);
				else if (loadEvent == WEBKIT_LOAD_FINISHED)
					w.LoadFinished?.Invoke (w, EventArgs.Empty);
			} catch (Exception ex) {
				GLib.ExceptionManager.RaiseUnhandledException (ex, false);
			}
		}

		static bool OnDecidePolicy (IntPtr view, IntPtr decision, int decisionType, IntPtr data)
		{
			try {
				var w = GLib.Object.TryGetObject (view) as WebView;
				if (w == null || w.NavigationRequested == null)
					return false;
				if (decisionType != WEBKIT_POLICY_DECISION_TYPE_NAVIGATION_ACTION && decisionType != WEBKIT_POLICY_DECISION_TYPE_NEW_WINDOW_ACTION)
					return false;
				if (webkit_navigation_policy_decision_get_navigation_action == null || webkit_navigation_action_get_request == null || webkit_uri_request_get_uri == null)
					return false;
				var action = webkit_navigation_policy_decision_get_navigation_action (decision);
				var request = action != IntPtr.Zero ? webkit_navigation_action_get_request (action) : IntPtr.Zero;
				if (request == IntPtr.Zero)
					return false;
				var args = new NavigationRequestedArgs (new NetworkRequest (GLib.Marshaller.Utf8PtrToString (webkit_uri_request_get_uri (request))));
				w.NavigationRequested (w, args);
				switch (args.RetVal) {
				case NavigationResponse.Ignore:
					webkit_policy_decision_ignore?.Invoke (decision);
					return true;
				case NavigationResponse.Download:
					if (webkit_policy_decision_download == null)
						return false;
					webkit_policy_decision_download (decision);
					return true;
				default:
					return false;
				}
			} catch (Exception ex) {
				GLib.ExceptionManager.RaiseUnhandledException (ex, false);
				return false;
			}
		}

		static bool OnContextMenu (IntPtr view, IntPtr menu, IntPtr hitTestResult, IntPtr data)
		{
			try {
				var w = GLib.Object.TryGetObject (view) as WebView;
				if (w == null || w.ContextMenu == null)
					return false;
				var args = new ContextMenuArgs (hitTestResult);
				w.ContextMenu (w, args);
				return args.RetVal;
			} catch (Exception ex) {
				GLib.ExceptionManager.RaiseUnhandledException (ex, false);
				return false;
			}
		}

		static bool initialized = false;
		internal static void Initialize ()
		{
			if (initialized || !IsAvailable)
				return;

			initialized = true;
			GLib.GType.Register (WebView.GType, typeof (WebView));
		}

		public static new GLib.GType GType {
			get {
				return new GLib.GType (webkit_web_view_get_type ());
			}
		}

		static T Load<T> (string symbol) where T : class
		{
			return GtkInterop.LoadFunction<T> (NativeLib.Webkit, symbol);
		}

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate IntPtr d_ptr_void ();
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate IntPtr d_ptr_ptr (IntPtr raw);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate void d_void_ptr (IntPtr raw);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate void d_void_ptr_ptr (IntPtr raw, IntPtr a);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate void d_void_ptr_ptr_ptr (IntPtr raw, IntPtr a, IntPtr b);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate bool d_bool_ptr (IntPtr raw);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate double d_double_ptr (IntPtr raw);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate void d_void_ptr_rgba (IntPtr raw, ref Gdk.RGBA rgba);
		[UnmanagedFunctionPointer (CallingConvention.Cdecl)] delegate IntPtr d_g_signal_connect_data (IntPtr instance, IntPtr detailed_signal, IntPtr handler, IntPtr data, IntPtr destroy_data, int connect_flags);

		static readonly d_ptr_void webkit_web_view_new = Load<d_ptr_void> ("webkit_web_view_new");
		static readonly d_ptr_void webkit_web_view_get_type = Load<d_ptr_void> ("webkit_web_view_get_type");
		static readonly d_void_ptr_ptr webkit_web_view_load_uri = Load<d_void_ptr_ptr> ("webkit_web_view_load_uri");
		static readonly d_void_ptr_ptr_ptr webkit_web_view_load_html = Load<d_void_ptr_ptr_ptr> ("webkit_web_view_load_html");
		static readonly d_ptr_ptr webkit_web_view_get_uri = Load<d_ptr_ptr> ("webkit_web_view_get_uri");
		static readonly d_ptr_ptr webkit_web_view_get_title = Load<d_ptr_ptr> ("webkit_web_view_get_title");
		static readonly d_ptr_ptr webkit_web_view_get_settings = Load<d_ptr_ptr> ("webkit_web_view_get_settings");
		static readonly d_double_ptr webkit_web_view_get_estimated_load_progress = Load<d_double_ptr> ("webkit_web_view_get_estimated_load_progress");
		static readonly d_void_ptr webkit_web_view_stop_loading = Load<d_void_ptr> ("webkit_web_view_stop_loading");
		static readonly d_void_ptr webkit_web_view_reload = Load<d_void_ptr> ("webkit_web_view_reload");
		static readonly d_bool_ptr webkit_web_view_can_go_back = Load<d_bool_ptr> ("webkit_web_view_can_go_back");
		static readonly d_void_ptr webkit_web_view_go_back = Load<d_void_ptr> ("webkit_web_view_go_back");
		static readonly d_bool_ptr webkit_web_view_can_go_forward = Load<d_bool_ptr> ("webkit_web_view_can_go_forward");
		static readonly d_void_ptr webkit_web_view_go_forward = Load<d_void_ptr> ("webkit_web_view_go_forward");
		static readonly d_void_ptr_rgba webkit_web_view_get_background_color = Load<d_void_ptr_rgba> ("webkit_web_view_get_background_color");
		static readonly d_void_ptr_rgba webkit_web_view_set_background_color = Load<d_void_ptr_rgba> ("webkit_web_view_set_background_color");
		static readonly d_ptr_ptr webkit_navigation_policy_decision_get_navigation_action = Load<d_ptr_ptr> ("webkit_navigation_policy_decision_get_navigation_action");
		static readonly d_ptr_ptr webkit_navigation_action_get_request = Load<d_ptr_ptr> ("webkit_navigation_action_get_request");
		static readonly d_ptr_ptr webkit_uri_request_get_uri = Load<d_ptr_ptr> ("webkit_uri_request_get_uri");
		static readonly d_void_ptr webkit_policy_decision_ignore = Load<d_void_ptr> ("webkit_policy_decision_ignore");
		static readonly d_void_ptr webkit_policy_decision_download = Load<d_void_ptr> ("webkit_policy_decision_download");
		// g_signal_connect_data is not exposed by GLibSharp; the handler id (gulong) is not needed
		static readonly d_g_signal_connect_data g_signal_connect_data = GtkInterop.LoadFunction<d_g_signal_connect_data> (NativeLib.GObject, "g_signal_connect_data");
	}

	/// <summary>The request of a navigation (WebKitURIRequest URI).</summary>
	public sealed class NetworkRequest
	{
		public NetworkRequest (string uri)
		{
			Uri = uri;
		}

		public string Uri { get; set; }
	}

	public enum NavigationResponse {

		Accept,
		Ignore,
		Download,
	}

	public class LoadProgressChangedArgs : EventArgs
	{
		public LoadProgressChangedArgs (int progress)
		{
			Progress = progress;
		}

		public int Progress { get; private set; }
	}

	public class NavigationRequestedArgs : EventArgs
	{
		public NavigationRequestedArgs (NetworkRequest request)
		{
			Request = request;
		}

		public NetworkRequest Request { get; private set; }

		/// <summary>The policy applied to the navigation (default: Accept).</summary>
		public NavigationResponse RetVal { get; set; }
	}

	public class TitleChangedArgs : EventArgs
	{
		public TitleChangedArgs (string title)
		{
			Title = title;
		}

		public string Title { get; private set; }
	}

	public class ContextMenuArgs : EventArgs
	{
		public ContextMenuArgs (IntPtr hitTestResult)
		{
			HitTestResult = hitTestResult;
		}

		/// <summary>The native WebKitHitTestResult*.</summary>
		public IntPtr HitTestResult { get; private set; }

		/// <summary>True suppresses the context menu.</summary>
		public bool RetVal { get; set; }
	}
}
