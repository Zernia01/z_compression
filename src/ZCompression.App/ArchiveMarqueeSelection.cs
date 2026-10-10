using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ZCompression.App;

// Blank-space gestures select rows; gestures starting on a row remain archive exports.
internal sealed class ArchiveMarqueeSelection
{
    private readonly DataGrid _grid;
    private readonly Func<bool> _canSelect;
    private Point? _origin;
    private Rect _viewport;
    private object[] _previous = [];
    private object[] _baseline = [];
    private SelectionAdorner? _adorner;

    public ArchiveMarqueeSelection(DataGrid grid, Func<bool> canSelect)
    {
        _grid = grid;
        _canSelect = canSelect;
        grid.PreviewMouseLeftButtonDown += OnDown;
        grid.PreviewMouseMove += OnMove;
        grid.PreviewMouseLeftButtonUp += (_, e) => { if (_origin is not null) { Finish(); e.Handled = true; } };
        grid.LostMouseCapture += (_, _) => Finish();
        grid.PreviewKeyDown += (_, e) =>
        {
            if (_origin is null || e.Key != Key.Escape) return;
            SetSelection(_previous);
            Finish();
            e.Handled = true;
        };
        grid.Unloaded += (_, _) => Finish();
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || e.ClickCount != 1 || !_canSelect()) return;
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = Parent(element))
            if (element is DataGridRow or DataGridColumnHeader or ScrollBar or ButtonBase) return;
        var presenter = Descendants(_grid).OfType<ScrollContentPresenter>().FirstOrDefault();
        if (presenter is null) return;
        var viewport = presenter.TransformToAncestor(_grid).TransformBounds(new Rect(presenter.RenderSize));
        var point = e.GetPosition(_grid);
        if (!viewport.Contains(point)) return;
        if (!_grid.CaptureMouse()) return;
        _grid.Focus();
        Begin(point, viewport, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        e.Handled = true;
    }

    internal void Begin(Point origin, Rect viewport, bool additive)
    {
        _origin = origin;
        _viewport = viewport;
        _previous = _grid.SelectedItems.Cast<object>().ToArray();
        _baseline = additive ? _previous : [];
        SetSelection(_baseline);
        var layer = AdornerLayer.GetAdornerLayer(_grid);
        if (layer is not null)
        {
            _adorner = new SelectionAdorner(_grid, viewport) { IsHitTestVisible = false };
            layer.Add(_adorner);
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_origin is null) return;
        if (e.LeftButton != MouseButtonState.Pressed || !_canSelect()) { Finish(); return; }
        Update(e.GetPosition(_grid));
        e.Handled = true;
    }

    internal void Update(Point point)
    {
        if (_origin is not { } origin) return;
        point = new Point(Math.Clamp(point.X, _viewport.Left, _viewport.Right), Math.Clamp(point.Y, _viewport.Top, _viewport.Bottom));
        var rectangle = new Rect(origin, point);
        var selection = new HashSet<object>(_baseline);
        foreach (var row in Descendants(_grid).OfType<DataGridRow>())
        {
            if (row.Item is not ArchiveBrowserItem { IsParent: false } || !row.IsVisible) continue;
            var bounds = row.TransformToAncestor(_grid).TransformBounds(new Rect(row.RenderSize));
            bounds.Intersect(_viewport);
            if (!bounds.IsEmpty && rectangle.Width > 0 && rectangle.Height > 0 && rectangle.IntersectsWith(bounds)) selection.Add(row.Item);
        }
        SetSelection(selection);
        if (_adorner is not null) { _adorner.Rectangle = rectangle; _adorner.InvalidateVisual(); }
    }

    private void SetSelection(IEnumerable<object> items)
    {
        var desired = new HashSet<object>(items);
        foreach (var item in _grid.SelectedItems.Cast<object>().ToArray())
            if (!desired.Contains(item)) _grid.SelectedItems.Remove(item);
        foreach (var item in desired)
            if (!_grid.SelectedItems.Contains(item) && _grid.Items.Contains(item)) _grid.SelectedItems.Add(item);
    }

    internal void Finish()
    {
        _origin = null;
        if (_adorner is not null) AdornerLayer.GetAdornerLayer(_grid)?.Remove(_adorner);
        _adorner = null;
        _previous = _baseline = [];
        if (_grid.IsMouseCaptured) _grid.ReleaseMouseCapture();
    }

    private static DependencyObject? Parent(DependencyObject element) => element is Visual or System.Windows.Media.Media3D.Visual3D
        ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class SelectionAdorner(UIElement element, Rect viewport) : Adorner(element)
    {
        public Rect Rectangle { get; set; }
        protected override void OnRender(DrawingContext context)
        {
            var accent = (AdornedElement as FrameworkElement)?.TryFindResource("AccentBrush") as SolidColorBrush ?? Brushes.DodgerBlue;
            context.PushClip(new RectangleGeometry(viewport));
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(45, accent.Color.R, accent.Color.G, accent.Color.B)), new Pen(accent, 1), Rectangle);
            context.Pop();
        }
    }
}
