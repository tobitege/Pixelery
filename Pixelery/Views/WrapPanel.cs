namespace Pixelery.Views
{
    /// <summary>
    /// Wraps children into rows. With <see cref="MinItemWidth"/> set, items share each row in equal columns
    /// (card grids); otherwise every child keeps its desired width (chips).
    /// </summary>
    public sealed partial class WrapPanel : Panel
    {
        public double HorizontalSpacing { get; set; } = 8;

        public double VerticalSpacing { get; set; } = 8;

        /// <summary>Minimum column width for uniform grids; 0 keeps natural widths.</summary>
        public double MinItemWidth { get; set; }

        /// <summary>Fixed number of columns; 0 derives the count from the available width.</summary>
        public int ColumnCount { get; set; }

        private int Columns(double available) =>
            ColumnCount > 0 ? ColumnCount : Math.Max(1, (int)((available + HorizontalSpacing) / (MinItemWidth + HorizontalSpacing)));

        private double ColumnWidth(double available, int columns) =>
            Math.Max(0, (available - HorizontalSpacing * (columns - 1)) / columns);

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
            if (MinItemWidth > 0)
            {
                var columns = Columns(width);
                var columnWidth = ColumnWidth(width, columns);
                double total = 0, rowHeight = 0;
                for (var i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.Measure(new Size(columnWidth, double.PositiveInfinity));
                    rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                    if (i % columns == columns - 1 || i == Children.Count - 1)
                    {
                        total += rowHeight + (total > 0 ? VerticalSpacing : 0);
                        rowHeight = 0;
                    }
                }
                return new Size(width, total);
            }

            double x = 0, y = 0, line = 0, maxX = 0;
            foreach (var child in Children)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                var size = child.DesiredSize;
                if (x > 0 && x + size.Width > width)
                {
                    y += line + VerticalSpacing;
                    x = 0;
                    line = 0;
                }
                x += size.Width + HorizontalSpacing;
                maxX = Math.Max(maxX, x - HorizontalSpacing);
                line = Math.Max(line, size.Height);
            }
            return new Size(double.IsInfinity(availableSize.Width) ? maxX : width, y + line);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = finalSize.Width;
            if (MinItemWidth > 0)
            {
                var columns = Columns(width);
                var columnWidth = ColumnWidth(width, columns);
                double y = 0;
                for (var start = 0; start < Children.Count; start += columns)
                {
                    double rowHeight = 0;
                    for (var i = start; i < Math.Min(start + columns, Children.Count); i++)
                        rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
                    for (var i = start; i < Math.Min(start + columns, Children.Count); i++)
                    {
                        var col = i - start;
                        Children[i].Arrange(new Rect(col * (columnWidth + HorizontalSpacing), y, columnWidth, rowHeight));
                    }
                    y += rowHeight + VerticalSpacing;
                }
                return finalSize;
            }

            double x = 0, top = 0, line = 0;
            foreach (var child in Children)
            {
                var size = child.DesiredSize;
                if (x > 0 && x + size.Width > width)
                {
                    top += line + VerticalSpacing;
                    x = 0;
                    line = 0;
                }
                child.Arrange(new Rect(x, top, size.Width, size.Height));
                x += size.Width + HorizontalSpacing;
                line = Math.Max(line, size.Height);
            }
            return finalSize;
        }
    }
}
