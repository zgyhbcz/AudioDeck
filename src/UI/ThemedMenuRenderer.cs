using System.Drawing;
using System.Windows.Forms;

namespace AudioDeck.UI
{
    internal sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly Theme _theme;

        public ThemedColorTable(Theme theme)
        {
            _theme = theme;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground { get { return _theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return _theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return _theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return _theme.Card; } }
        public override Color MenuBorder { get { return _theme.Border; } }
        public override Color MenuItemBorder { get { return _theme.Hover; } }
        public override Color MenuItemSelected { get { return _theme.Hover; } }
        public override Color SeparatorDark { get { return _theme.Border; } }
        public override Color SeparatorLight { get { return _theme.Border; } }
        public override Color ToolStripBorder { get { return _theme.Border; } }
        public override Color CheckBackground { get { return _theme.Card; } }
        public override Color CheckSelectedBackground { get { return _theme.Hover; } }
        public override Color CheckPressedBackground { get { return _theme.Hover; } }
    }

    internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Theme _theme;

        public ThemedMenuRenderer(Theme theme)
            : base(new ThemedColorTable(theme))
        {
            _theme = theme;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _theme.Text : _theme.TextSecondary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _theme.TextSecondary;
            base.OnRenderArrow(e);
        }
    }
}
