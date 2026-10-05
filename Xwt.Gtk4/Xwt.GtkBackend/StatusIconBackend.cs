// 
// StatusIconBackend.cs
//  
// Author:
//       Antony Denyer <antonydenyer@gmail.com>
// 
// Copyright (c) 2012 7digital Media Ltd
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
	/// GTK 4: there is no status icon (GtkStatusIcon was removed and GTK has no system-tray API).
	/// The backend keeps the API working without showing anything.
	/// </summary>
	public class StatusIconBackend : IStatusIconBackend
	{
		public void InitializeBackend (object frontend, ApplicationContext context)
		{
		}

		public void SetImage (ImageDescription image)
		{
		}

		public void SetMenu (object menuBackend)
		{
			if (menuBackend == null) {
				throw new ArgumentNullException ("menuBackend");
			}
		}

		public void Dispose ()
		{
		}

		public void EnableEvent (object eventId) { }
		public void DisableEvent (object eventId) { }
	}
}
