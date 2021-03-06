﻿using System;
using MonoTouch.UIKit;

namespace Toggl.Ross.Theme
{
    public static partial class Style
    {
        public static class TagList
        {
            public static void RowBackground (UIView v)
            {
                v.BackgroundColor = Color.White;
            }

            public static void NameLabel (UILabel v)
            {
                v.Font = UIFont.FromName ("HelveticaNeue", 17f);
                v.TextAlignment = UITextAlignment.Left;
                v.TextColor = Color.Black;
            }
        }
    }
}
