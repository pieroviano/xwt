// 
// ClipboardBackend.cs
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
using System.Linq;
using Xwt.Backends;
using System.Threading;


namespace Xwt.GtkBackend
{
	/// <summary>
	/// Clipboard on <see cref="Gdk.Clipboard"/>. GTK 4 reads the clipboard asynchronously; the
	/// synchronous Xwt API waits in a nested main loop, except for data set by this process,
	/// which is returned directly.
	/// </summary>
	public class GtkClipboardBackend: ClipboardBackend
	{
		Gdk.Clipboard clipboard;
		Gdk.ContentProvider ownProvider;
		TransferDataType ownType;
		object ownValue;

		public GtkClipboardBackend ()
		{
			clipboard = Gdk.Display.Default.Clipboard;
		}

		bool HasOwnData {
			get {
				if (ownProvider == null || !clipboard.IsLocal)
					return false;
				var content = clipboard.Content;
				return content != null && content.Handle == ownProvider.Handle;
			}
		}

		#region IClipboardBackend implementation
		public override void Clear ()
		{
			ownProvider = null;
			ownValue = null;
			clipboard.SetContent (null);
		}

		/// <summary>
		/// Sets the clipboard content. GTK 4 content providers hold their data, so the data source
		/// is evaluated now rather than when the data is requested.
		/// </summary>
		public override void SetData (TransferDataType type, Func<object> dataSource)
		{
			var value = dataSource ();
			if (value == null) {
				Clear ();
				return;
			}
			var providers = Util.CreateContentProviders (type, value).ToArray ();
			var provider = providers.Length == 1 ? providers [0] : new Gdk.ContentProvider (providers);
			ownType = type;
			ownValue = value;
			ownProvider = provider;
			clipboard.SetContent (provider);
		}

		public override bool IsTypeAvailable (TransferDataType type)
		{
			if (HasOwnData)
				return ownType == type;
			var formats = clipboard.Formats;
			return formats != null && Util.GetDragTypes (formats).Contains (type);
		}

		public override object GetData (TransferDataType type)
		{
			if (HasOwnData)
				return ownType == type ? ownValue : null;

			var store = new TransferDataStore ();
			var loop = new NestedLoop ();
			Util.ReadTransferData (ApplicationContext, clipboard, new [] { type }, store, loop.Quit);
			loop.Run ();
			return ((ITransferData)store).GetValue (type);
		}

		public override IAsyncResult BeginGetData (TransferDataType type, AsyncCallback callback, object state)
		{
			var request = new DataRequest (callback, state);
			if (HasOwnData) {
				request.SetComplete (ownType == type ? ownValue : null);
				return request;
			}
			var store = new TransferDataStore ();
			Util.ReadTransferData (ApplicationContext, clipboard, new [] { type }, store, () => request.SetComplete (((ITransferData)store).GetValue (type)));
			return request;
		}

		public override object EndGetData (IAsyncResult ares)
		{
			return ((DataRequest)ares).Result;
		}

		#endregion
	}

	class DataRequest: IAsyncResult
	{
		readonly object sync = new object ();
		ManualResetEvent doneEvent;
		bool complete;
		AsyncCallback callback;

		public DataRequest (AsyncCallback callback, object state)
		{
			this.callback = callback;
			AsyncState = state;
		}

		public object Result { get; set; }

		public void SetComplete (object result)
		{
			Result = result;
			lock (sync) {
				complete = true;
				if (doneEvent != null)
					doneEvent.Set ();
			}
			if (callback != null) {
				Application.Invoke (delegate {
					callback (this);
				});
			}
		}

		#region IAsyncResult implementation
		public object AsyncState { get; set; }

		public WaitHandle AsyncWaitHandle {
			get {
				lock (sync) {
					if (doneEvent == null)
						doneEvent = new ManualResetEvent (complete);
				}
				return doneEvent;
			}
		}

		public bool CompletedSynchronously {
			get {
				return false;
			}
		}

		public bool IsCompleted {
			get {
				return complete;
			}
		}
		#endregion
	}
}
