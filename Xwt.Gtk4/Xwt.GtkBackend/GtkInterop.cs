//
// PangoUtils.cs
//
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
//
// Copyright (c) 2010 Novell, Inc. (http://www.novell.com)
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
using System.IO;
using System.Runtime.InteropServices;
using Xwt.Drawing;

namespace Xwt.GtkBackend
{
	/// <summary>Native libraries of the GTK 4 stack, for the few symbols GtkSharp4 does not bind.</summary>
	public enum NativeLib
	{
		GLib,
		GObject,
		Gio,
		Pango,
		PangoCairo,
		Cairo,
		GdkPixbuf,
		Gtk,
		Webkit,
	}

	/// <summary>
	/// Resolves native symbols the same way GtkSharp4 does (library handle + symbol lookup), so no
	/// platform-specific DllImport names are baked in. GtkSharp4 has already loaded these libraries,
	/// so the lookups return the existing modules.
	/// </summary>
	public static class GtkInterop
	{
		static readonly Dictionary<NativeLib, string[]> names = new Dictionary<NativeLib, string[]> {
			{ NativeLib.GLib, new[] { "glib-2.0-0.dll", "libglib-2.0.so.0", "libglib-2.0.0.dylib", "libglib-2.0-0.dll" } },
			{ NativeLib.GObject, new[] { "gobject-2.0-0.dll", "libgobject-2.0.so.0", "libgobject-2.0.0.dylib", "libgobject-2.0-0.dll" } },
			{ NativeLib.Gio, new[] { "gio-2.0-0.dll", "libgio-2.0.so.0", "libgio-2.0.0.dylib", "libgio-2.0-0.dll" } },
			{ NativeLib.Pango, new[] { "pango-1.0-0.dll", "libpango-1.0.so.0", "libpango-1.0.0.dylib", "libpango-1.0-0.dll" } },
			{ NativeLib.PangoCairo, new[] { "pangocairo-1.0-0.dll", "libpangocairo-1.0.so.0", "libpangocairo-1.0.0.dylib", "libpangocairo-1.0-0.dll" } },
			{ NativeLib.Cairo, new[] { "cairo-2.dll", "libcairo.so.2", "libcairo.2.dylib", "libcairo-2.dll" } },
			{ NativeLib.GdkPixbuf, new[] { "gdk_pixbuf-2.0-0.dll", "libgdk_pixbuf-2.0.so.0", "libgdk_pixbuf-2.0.dylib", "libgdk_pixbuf-2.0-0.dll" } },
			{ NativeLib.Gtk, new[] { "gtk-4-1.dll", "libgtk-4.so.1", "libgtk-4.1.dylib", "libgtk-4-1.dll" } },
			{ NativeLib.Webkit, new[] { "webkitgtk-6.0-4.dll", "libwebkitgtk-6.0.so.4", "libwebkitgtk-6.0.dylib" } },
		};
		static readonly Dictionary<NativeLib, IntPtr> handles = new Dictionary<NativeLib, IntPtr> ();

		[DllImport ("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode)]
		static extern IntPtr LoadLibraryW (string name);
		[DllImport ("kernel32.dll", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi)]
		static extern IntPtr GetProcAddressW (IntPtr module, string name);
		[DllImport ("libdl.so.2", EntryPoint = "dlopen")]
		static extern IntPtr dlopen_linux (string path, int flags);
		[DllImport ("libdl.so.2", EntryPoint = "dlsym")]
		static extern IntPtr dlsym_linux (IntPtr handle, string symbol);
		[DllImport ("/usr/lib/libSystem.dylib", EntryPoint = "dlopen")]
		static extern IntPtr dlopen_mac (string path, int flags);
		[DllImport ("/usr/lib/libSystem.dylib", EntryPoint = "dlsym")]
		static extern IntPtr dlsym_mac (IntPtr handle, string symbol);

		static IntPtr Open (string name)
		{
			try {
				if (Platform.IsWindows)
					return LoadLibraryW (name);
				if (Platform.IsMac)
					return dlopen_mac (name, 1);
				return dlopen_linux (name, 1);
			} catch (DllNotFoundException) {
				return IntPtr.Zero;
			} catch (EntryPointNotFoundException) {
				return IntPtr.Zero;
			}
		}

		static IntPtr GetLibrary (NativeLib lib)
		{
			lock (handles) {
				IntPtr h;
				if (handles.TryGetValue (lib, out h))
					return h;
				var candidates = names [lib];
				int preferred = Platform.IsWindows ? 0 : Platform.IsMac ? 2 : 1;
				h = preferred < candidates.Length ? Open (candidates [preferred]) : IntPtr.Zero;
				if (h == IntPtr.Zero && Platform.IsWindows) {
					var dir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Gtk", "4.22.4", "bin");
					h = Open (Path.Combine (dir, candidates [0]));
				}
				for (int i = 0; h == IntPtr.Zero && i < candidates.Length; i++)
					h = Open (candidates [i]);
				handles [lib] = h;
				return h;
			}
		}

		/// <summary>Returns the native function as a delegate, or null if the library or symbol is missing.</summary>
		public static T LoadFunction<T> (NativeLib lib, string symbol) where T : class
		{
			var h = GetLibrary (lib);
			if (h == IntPtr.Zero)
				return null;
			IntPtr f;
			if (Platform.IsWindows)
				f = GetProcAddressW (h, symbol);
			else if (Platform.IsMac)
				f = dlsym_mac (h, symbol);
			else
				f = dlsym_linux (h, symbol);
			if (f == IntPtr.Zero)
				return null;
			return Marshal.GetDelegateForFunctionPointer (f, typeof (T)) as T;
		}

		public static bool IsAvailable (NativeLib lib)
		{
			return GetLibrary (lib) != IntPtr.Zero;
		}
	}

	/// <summary>
	/// Builds a Pango attribute list from Xwt text attributes.
	/// </summary>
	internal class FastPangoAttrList : IDisposable
	{
		const float PangoScale = 1024;

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate IntPtr d_pango_attr_size_new_absolute (int size);
		// PangoSharp4's AttrSize(int, bool absolute) maps the flag backwards, so the absolute size
		// attribute is created directly.
		static readonly d_pango_attr_size_new_absolute pango_attr_size_new_absolute =
			GtkInterop.LoadFunction<d_pango_attr_size_new_absolute> (NativeLib.Pango, "pango_attr_size_new_absolute");

		Pango.AttrList list;
		public Pango.Color DefaultLinkColor = Toolkit.CurrentEngine.Defaults.FallbackLinkColor.ToPangoColor ();

		public FastPangoAttrList ()
		{
			list = new Pango.AttrList ();
		}

		public Pango.AttrList List {
			get { return list; }
		}

		public void AddAttributes (TextIndexer indexer, IEnumerable<TextAttribute> attrs)
		{
			foreach (var attr in attrs)
				AddAttribute (indexer, attr);
		}

		public void AddAttribute (TextIndexer indexer, TextAttribute attr)
		{
			var start = (uint) indexer.IndexToByteIndex (attr.StartIndex);
			var end = (uint) indexer.IndexToByteIndex (attr.StartIndex + attr.Count);

			if (attr is BackgroundTextAttribute) {
				var xa = (BackgroundTextAttribute)attr;
				Add (new Pango.AttrBackground (xa.Color.ToPangoColor ()), start, end);
			}
			else if (attr is ColorTextAttribute) {
				var xa = (ColorTextAttribute)attr;
				Add (new Pango.AttrForeground (xa.Color.ToPangoColor ()), start, end);
			}
			else if (attr is FontWeightTextAttribute) {
				var xa = (FontWeightTextAttribute)attr;
				Add (new Pango.AttrWeight ((Pango.Weight)(int)xa.Weight), start, end);
			}
			else if (attr is FontSizeTextAttribute) {
				var xa = (FontSizeTextAttribute)attr;
				AddFontSizeAttribute ((int) (xa.Size * PangoScale), start, end);
			}
			else if (attr is FontStyleTextAttribute) {
				var xa = (FontStyleTextAttribute)attr;
				Add (new Pango.AttrStyle ((Pango.Style)(int)xa.Style), start, end);
			}
			else if (attr is UnderlineTextAttribute) {
				var xa = (UnderlineTextAttribute)attr;
				Add (new Pango.AttrUnderline (xa.Underline ? Pango.Underline.Single : Pango.Underline.None), start, end);
			}
			else if (attr is StrikethroughTextAttribute) {
				var xa = (StrikethroughTextAttribute)attr;
				Add (new Pango.AttrStrikethrough (xa.Strikethrough), start, end);
			}
			else if (attr is FontTextAttribute) {
				var xa = (FontTextAttribute)attr;
				Add (new Pango.AttrFontDesc ((Pango.FontDescription)Toolkit.GetBackend (xa.Font)), start, end);
			}
			else if (attr is LinkTextAttribute) {
				Add (new Pango.AttrUnderline (Pango.Underline.Single), start, end);
				Add (new Pango.AttrForeground (DefaultLinkColor), start, end);
			}
		}

		public void AddFontSizeAttribute (int size, uint start, uint end)
		{
			var raw = pango_attr_size_new_absolute (size);
			Add (Pango.Attribute.GetAttribute (raw, true), start, end);
		}

		void Add (Pango.Attribute attribute, uint start, uint end)
		{
			attribute.StartIndex = start;
			attribute.EndIndex = end;
			// AttrList.Insert copies the attribute
			list.Insert (attribute);
			attribute.Dispose ();
		}

		public void Dispose ()
		{
			list = null;
		}
	}

	/// <summary>
	/// "event" signal of Gtk.EventControllerLegacy, which the GtkSharp4 package does not bind
	/// (the controller is exposed without its signal).
	/// </summary>
	internal static class LegacyEvents
	{
		/// <summary>Returns true when the event has been handled (stops its propagation).</summary>
		public delegate bool Handler (Gdk.Event evt);

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		delegate bool EventSignalDelegate (IntPtr controller, IntPtr evt, IntPtr gch);

		static readonly EventSignalDelegate eventCallback = EventCallback;

		static bool EventCallback (IntPtr controller, IntPtr evt, IntPtr gch)
		{
			try {
				var sig = ((GCHandle) gch).Target as GLib.Signal;
				var handler = sig?.Handler as Handler;
				// the event is borrowed: the wrapper does not own (nor unref) it
				return handler != null && evt != IntPtr.Zero && handler (new Gdk.Event (evt));
			} catch (Exception e) {
				GLib.ExceptionManager.RaiseUnhandledException (e, false);
				return false;
			}
		}

		public static void Connect (Gtk.EventControllerLegacy controller, Handler handler)
		{
			controller.AddSignalHandler ("event", handler, eventCallback);
		}

		public static void Disconnect (Gtk.EventControllerLegacy controller, Handler handler)
		{
			controller.RemoveSignalHandler ("event", handler);
		}
	}

	public class TextIndexer
	{
		static readonly List<int> emptyList = new List<int> ();
		static readonly int [] emptyArray = new int [0];
		int [] indexToByteIndex;
		List<int> byteIndexToIndex;

		public TextIndexer (string text)
		{
			SetupTables (text);
		}

		public int IndexToByteIndex (int i)
		{
			if (i >= indexToByteIndex.Length)
				// if the index exceeds the byte index range, return the last byte index + 1
				// telling pango to span the attribute to the end of the string
				// this happens if the string contains multibyte characters
				return indexToByteIndex[indexToByteIndex.Length-1] + 1;
			return indexToByteIndex[i];
		}

		public int ByteIndexToIndex (int i)
		{
			return byteIndexToIndex[i];
		}

		public void SetupTables (string text)
		{
			if (string.IsNullOrEmpty (text)) {
				this.indexToByteIndex = emptyArray;
				this.byteIndexToIndex = emptyList;
				return;
			}

			int byteIndex = 0;
			int [] indexToByteIndex = new int [text.Length];
			var byteIndexToIndex = new System.Collections.Generic.List<int> (text.Length);
			unsafe {
				fixed (char* p = text) {
					for (int i = 0; i < text.Length; i++) {
						indexToByteIndex[i] = byteIndex;
						byteIndex += System.Text.Encoding.UTF8.GetByteCount (p + i, 1);
						while (byteIndexToIndex.Count < byteIndex)
							byteIndexToIndex.Add (i);
					}
				}
			}
			this.indexToByteIndex = indexToByteIndex;
			this.byteIndexToIndex = byteIndexToIndex;
		}
	}
}
