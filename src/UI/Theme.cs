using System;
using System.Drawing;
using Microsoft.Win32;

namespace AudioDeck.UI
{
    internal sealed class Theme
    {
        public bool IsDark;
        public Color Background;
        public Color Card;
        public Color CardAlt;
        public Color Hover;
        public Color Text;
        public Color TextSecondary;
        public Color Border;
        public Color Accent;
        public Color AccentSoft;
        public Color AccentText;
        public Color Danger;
        public Color SliderTrack;
        public Color RowSelected;
        public Color RowSelectedBorder;

        public static Theme Create()
        {
            object value = null;
            try
            {
                value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
            }
            catch
            {
            }

            bool light = true;
            try
            {
                if (value != null)
                {
                    light = Convert.ToInt32(value) != 0;
                }
            }
            catch
            {
            }

            return light ? CreateLight() : CreateDark();
        }

        private static Theme CreateLight()
        {
            Theme t = new Theme();
            t.IsDark = false;
            t.Background = Color.FromArgb(246, 246, 249);
            t.Card = Color.FromArgb(255, 255, 255);
            t.CardAlt = Color.FromArgb(242, 245, 249);
            t.Hover = Color.FromArgb(239, 243, 248);
            t.Text = Color.FromArgb(26, 26, 30);
            t.TextSecondary = Color.FromArgb(105, 105, 117);
            t.Border = Color.FromArgb(226, 227, 234);
            t.Accent = Color.FromArgb(15, 108, 189);
            t.AccentSoft = Color.FromArgb(232, 242, 252);
            t.AccentText = Color.White;
            t.Danger = Color.FromArgb(196, 43, 28);
            t.SliderTrack = Color.FromArgb(216, 218, 225);
            t.RowSelected = Color.FromArgb(235, 244, 253);
            t.RowSelectedBorder = Color.FromArgb(183, 216, 243);
            return t;
        }

        private static Theme CreateDark()
        {
            Theme t = new Theme();
            t.IsDark = true;
            t.Background = Color.FromArgb(27, 27, 31);
            t.Card = Color.FromArgb(38, 38, 43);
            t.CardAlt = Color.FromArgb(48, 48, 54);
            t.Hover = Color.FromArgb(52, 52, 59);
            t.Text = Color.FromArgb(245, 245, 247);
            t.TextSecondary = Color.FromArgb(166, 166, 177);
            t.Border = Color.FromArgb(58, 58, 66);
            t.Accent = Color.FromArgb(76, 194, 255);
            t.AccentSoft = Color.FromArgb(23, 57, 74);
            t.AccentText = Color.FromArgb(7, 35, 47);
            t.Danger = Color.FromArgb(255, 153, 164);
            t.SliderTrack = Color.FromArgb(70, 70, 78);
            t.RowSelected = Color.FromArgb(23, 56, 74);
            t.RowSelectedBorder = Color.FromArgb(47, 96, 122);
            return t;
        }
    }
}
