// 
// Util.cs
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
using Xwt.Drawing;

using Xwt.Backends;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace Xwt.GtkBackend
{
	public static class Util
	{
		const string TextMime = "text/plain;charset=utf-8";
		const string UriListMime = "text/uri-list";
		const string PngMime = "image/png";
		const string CustomMimePrefix = "application/x-xwt-";

		/// <summary>
		/// The data being dragged by a drag started in this process. Local drops read it directly
		/// instead of going through the (asynchronous) GDK serialization.
		/// </summary>
		internal static TransferDataSource CurrentDragSource;

		#region Transfer data (clipboard and drag and drop)

		/// <summary>MIME types used to exchange a transfer data type, preferred first.</summary>
		public static string[] GetMimeTypes (TransferDataType type)
		{
			if (type == TransferDataType.Text)
				return new [] { TextMime, "text/plain", "UTF8_STRING", "STRING" };
			if (type == TransferDataType.Uri)
				return new [] { UriListMime };
			if (type == TransferDataType.Image)
				return new [] { PngMime, "image/tiff", "image/jpeg", "image/bmp" };
			if (type == TransferDataType.Rtf)
				return new [] { "text/rtf", "application/rtf", "Rich Text Format" };
			if (type == TransferDataType.Html)
				return new [] { "text/html", "HTML Format" };
			return new [] { CustomMimePrefix + type.Id };
		}

		internal static TransferDataType MimeToType (string mime)
		{
			if (mime == null)
				return null;
			if (mime.StartsWith (CustomMimePrefix, StringComparison.Ordinal))
				return TransferDataType.FromId (mime.Substring (CustomMimePrefix.Length));
			foreach (var t in new [] { TransferDataType.Text, TransferDataType.Uri, TransferDataType.Image, TransferDataType.Rtf, TransferDataType.Html }) {
				if (GetMimeTypes (t).Contains (mime))
					return t;
			}
			return null;
		}

		public static Gdk.ContentFormats BuildContentFormats (TransferDataType[] types)
		{
			var mimes = new List<string> ();
			foreach (var t in types)
				mimes.AddRange (GetMimeTypes (t));
			var formats = new Gdk.ContentFormats (mimes.Distinct ().ToArray ());
			// GTK serializers convert text/files/textures from/to their GTypes
			if (types.Contains (TransferDataType.Text))
				formats = formats.Union (new Gdk.ContentFormats (GLib.GType.String));
			if (types.Contains (TransferDataType.Uri))
				formats = formats.Union (new Gdk.ContentFormats (Gdk.FileList.GType));
			if (types.Contains (TransferDataType.Image))
				formats = formats.Union (new Gdk.ContentFormats (Gdk.Texture.GType));
			return formats;
		}

		internal static TransferDataType[] GetDragTypes (Gdk.ContentFormats formats)
		{
			var types = new List<TransferDataType> ();
			if (formats == null)
				return types.ToArray ();
			ulong n;
			foreach (var mime in formats.GetMimeTypes (out n) ?? new string [0]) {
				var t = MimeToType (mime);
				if (t != null && !types.Contains (t))
					types.Add (t);
			}
			if (formats.ContainGtype (GLib.GType.String) && !types.Contains (TransferDataType.Text))
				types.Add (TransferDataType.Text);
			if (formats.ContainGtype (Gdk.FileList.GType) && !types.Contains (TransferDataType.Uri))
				types.Add (TransferDataType.Uri);
			if (formats.ContainGtype (Gdk.Texture.GType) && !types.Contains (TransferDataType.Image))
				types.Add (TransferDataType.Image);
			return types.ToArray ();
		}

		static Gdk.ContentProvider BytesProvider (string mime, byte[] data)
		{
			return new Gdk.ContentProvider (mime, new GLib.Bytes (data));
		}

		/// <summary>Content provider offering every value of the data source.</summary>
		public static Gdk.ContentProvider CreateContentProvider (TransferDataSource data)
		{
			var providers = new List<Gdk.ContentProvider> ();
			foreach (var t in data.DataTypes) {
				object val = data.GetValue (t);
				if (val == null)
					continue;
				providers.AddRange (CreateContentProviders (t, val));
			}
			if (providers.Count == 1)
				return providers [0];
			return new Gdk.ContentProvider (providers.ToArray ());
		}

		internal static IEnumerable<Gdk.ContentProvider> CreateContentProviders (TransferDataType type, object val)
		{
			if (val is string) {
				var s = (string)val;
				if (type == TransferDataType.Text) {
					yield return new Gdk.ContentProvider (new GLib.Value (s));
					yield return BytesProvider (TextMime, Encoding.UTF8.GetBytes (s));
				} else if (type == TransferDataType.Uri) {
					yield return BytesProvider (UriListMime, Encoding.UTF8.GetBytes (s + "\r\n"));
				} else {
					foreach (var mime in GetMimeTypes (type))
						yield return BytesProvider (mime, Encoding.UTF8.GetBytes (s));
				}
			}
			else if (val is Xwt.Drawing.Image) {
				var bmp = ((Image)val).ToBitmap ();
				var pix = ((GtkImage)Toolkit.GetBackend (bmp)).Frames [0].Pixbuf;
				yield return new Gdk.ContentProvider (new GLib.Value (new Gdk.Texture (pix)));
			}
			else if (val is Uri) {
				yield return BytesProvider (UriListMime, Encoding.UTF8.GetBytes (((Uri)val).AbsoluteUri + "\r\n"));
			}
			else if (val is Uri[]) {
				var list = string.Join ("", ((Uri[])val).Select (u => u.AbsoluteUri + "\r\n"));
				yield return BytesProvider (UriListMime, Encoding.UTF8.GetBytes (list));
			}
			else {
				yield return BytesProvider (CustomMimePrefix + type.Id, TransferDataSource.SerializeValue (val));
			}
		}

		/// <summary>Adds serialized data received for <paramref name="type"/> to the store.</summary>
		internal static void AddToStore (ApplicationContext context, TransferDataStore target, TransferDataType type, string mime, byte[] data)
		{
			if (data == null)
				return;
			if (type == TransferDataType.Text) {
				target.AddText (DecodeText (data));
			} else if (type == TransferDataType.Uri) {
				var uris = DecodeText (data).Split (new [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.Where (u => !u.StartsWith ("#", StringComparison.Ordinal))
					.Select (u => new Uri (u.Trim ())).ToArray ();
				target.AddUris (uris);
			} else if (type == TransferDataType.Image) {
				using (var loader = new Gdk.PixbufLoader (data)) {
					var pix = loader.Pixbuf;
					if (pix != null)
						target.AddImage (context.Toolkit.WrapImage (new GtkImage (pix)));
				}
			} else
				target.AddValue (type, data);
		}

		static string DecodeText (byte[] data)
		{
			var s = Encoding.UTF8.GetString (data);
			int nul = s.IndexOf ('\0');
			return nul >= 0 ? s.Substring (0, nul) : s;
		}

		/// <summary>Copies the values of an in-process data source to the store.</summary>
		internal static void AddToStore (TransferDataSource source, TransferDataType[] types, TransferDataStore target)
		{
			foreach (var t in source.DataTypes) {
				if (types != null && !types.Contains (t))
					continue;
				var val = source.GetValue (t);
				if (val == null)
					continue;
				if (t == TransferDataType.Text && val is string)
					target.AddText ((string)val);
				else if (val is Image)
					target.AddImage ((Image)val);
				else if (val is Uri)
					target.AddUris (new [] { (Uri)val });
				else if (val is Uri[])
					target.AddUris ((Uri[])val);
				else
					target.AddValue (t, val);
			}
		}

		/// <summary>
		/// Reads the dropped data of the requested types into the store, then calls
		/// <paramref name="done"/>. Data dragged from this process is copied synchronously.
		/// </summary>
		internal static void ReadTransferData (ApplicationContext context, Gdk.Drop drop, TransferDataType[] types, TransferDataStore store, Action done)
		{
			if (drop.Drag != null && CurrentDragSource != null) {
				AddToStore (CurrentDragSource, types, store);
				done ();
				return;
			}
			ReadTransferData (context, drop.Formats, types, store, done,
				(mimes, cb) => drop.ReadAsync (mimes, GLib.Priority.Default, null, cb),
				res => { string m; var s = drop.ReadFinish (res, out m); return Tuple.Create (s, m); });
		}

		/// <summary>
		/// Reads clipboard data of the requested types into the store, then calls <paramref name="done"/>.
		/// </summary>
		internal static void ReadTransferData (ApplicationContext context, Gdk.Clipboard clipboard, TransferDataType[] types, TransferDataStore store, Action done)
		{
			ReadTransferData (context, clipboard.Formats, types, store, done,
				(mimes, cb) => clipboard.ReadAsync (mimes, GLib.Priority.Default, null, cb),
				res => { string m; var s = clipboard.ReadFinish (res, out m); return Tuple.Create (s, m); });
		}

		static void ReadTransferData (ApplicationContext context, Gdk.ContentFormats formats, TransferDataType[] types, TransferDataStore store, Action done,
			Action<string[], GLib.AsyncReadyCallback> startRead, Func<GLib.IAsyncResult, Tuple<GLib.InputStream, string>> finishRead)
		{
			var available = GetDragTypes (formats);
			var pending = (types ?? available).Where (t => available.Contains (t)).ToList ();
			int remaining = pending.Count;
			if (remaining == 0) {
				done ();
				return;
			}
			foreach (var type in pending) {
				var t = type;
				ulong n;
				var offered = formats.GetMimeTypes (out n) ?? new string [0];
				var mimes = GetMimeTypes (t).Where (m => offered.Contains (m)).ToArray ();
				Action<string, byte[]> complete = (mime, data) => {
					try {
						AddToStore (context, store, t, mime, data);
					} catch (Exception ex) {
						Console.WriteLine (ex);
					}
					if (--remaining == 0)
						done ();
				};
				if (mimes.Length == 0) {
					complete (null, null);
					continue;
				}
				startRead (mimes, (source, res, data) => {
					GLib.InputStream stream = null;
					string mime = null;
					try {
						var r = finishRead (res);
						stream = r.Item1;
						mime = r.Item2;
					} catch (GLib.GException) {
					}
					if (stream == null)
						complete (null, null);
					else
						ReadAll (stream, bytes => complete (mime, bytes));
				});
			}
		}

		/// <summary>Reads a stream to the end asynchronously.</summary>
		internal static void ReadAll (GLib.InputStream stream, Action<byte[]> done)
		{
			var buffer = new MemoryStream ();
			GLib.AsyncReadyCallback cb = null;
			cb = (source, res, data) => {
				GLib.Bytes bytes = null;
				try {
					bytes = stream.ReadBytesFinish (res);
				} catch (GLib.GException) {
				}
				var chunk = bytes?.Data;
				if (chunk == null || chunk.Length == 0) {
					stream.Close (null);
					done (buffer.ToArray ());
					return;
				}
				buffer.Write (chunk, 0, chunk.Length);
				stream.ReadBytesAsync (65536, GLib.Priority.Default, null, cb);
			};
			stream.ReadBytesAsync (65536, GLib.Priority.Default, null, cb);
		}

		#endregion

		#region Stock icons

		static Dictionary<string,string> icons;

		/// <summary>Freedesktop icon name for an Xwt stock icon id.</summary>
		public static string GetStockIconName (string id)
		{
			if (icons == null) {
				icons = new Dictionary<string, string> ();
				icons [StockIconId.ZoomIn] = "zoom-in";
				icons [StockIconId.ZoomOut] = "zoom-out";
				icons [StockIconId.Zoom100] = "zoom-original";
				icons [StockIconId.ZoomFit] = "zoom-fit-best";
				icons [StockIconId.OrientationPortrait] = "object-rotate-left";
				icons [StockIconId.OrientationLandscape] = "object-rotate-right";
				icons [StockIconId.Add] = "list-add";
				icons [StockIconId.Remove] = "list-remove";
				icons [StockIconId.Warning] = "dialog-warning";
				icons [StockIconId.Error] = "dialog-error";
				icons [StockIconId.Information] = "dialog-information";
				icons [StockIconId.Question] = "dialog-question";
			}
			string res;
			if (!icons.TryGetValue (id, out res))
				throw new NotSupportedException ("Unknown image: " + id);
			return res;
		}

		public static GtkImage ToGtkStock (string id)
		{
			return new GtkImage (GetStockIconName (id));
		}

		public static ImageDescription WithDefaultSize (this ImageDescription image, Xwt.IconSize defaultIconSize)
		{
			if (image.Size.IsZero) {
				var s = defaultIconSize.ToPixelSize ();
				image.Size = new Size (s, s);
			}
			return image;
		}

		#endregion

		public static double GetScaleFactor (Gtk.Widget w)
		{
			return GtkWorkarounds.GetScaleFactor (w);
		}

		public static double GetDefaultScaleFactor ()
		{
			return GtkWorkarounds.GetDefaultScaleFactor ();
		}

		internal static void SetSourceColor (this Cairo.Context cr, Cairo.Color color)
		{
			cr.SetSourceRGBA (color.R, color.G, color.B, color.A);
		}

		internal static TextIndexer ApplyFormattedText(this Gtk.Label label, FormattedText text)
		{
			TextIndexer indexer = null;
			var list = new FastPangoAttrList ();
			if (text != null) {
				Gdk.RGBA link;
				if (label.StyleContext.LookupColor ("accent_color", out link) || label.StyleContext.LookupColor ("link_color", out link))
					list.DefaultLinkColor = link.ToXwtValue ().ToPangoColor ();
				indexer = new TextIndexer (text.Text);
				list.AddAttributes (indexer, text.Attributes);
			}
			label.Attributes = list.List;

			return indexer;
		}
	}
}
