using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZCompression.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        var items = new[]
        {
            Item("..", true), Item("folder"), Item("one.txt"), Item("two.txt"), Item("last.txt")
        };
        var grid = new DataGrid
        {
            ItemsSource = items, AutoGenerateColumns = false, RowHeight = 30,
            SelectionMode = DataGridSelectionMode.Extended, SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column, IsReadOnly = true
        };
        grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding("Name"), Width = 400 });
        var window = new Window { Style = new Style(typeof(Window)), Content = grid, Width = 500, Height = 400, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        window.Show(); grid.UpdateLayout();
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(items[2]);
        if (row is null) throw new Exception("Rows were not realized.");
        var rowBounds = row.TransformToAncestor(grid).TransformBounds(new Rect(row.RenderSize));
        var selection = new ArchiveMarqueeSelection(grid, () => true);
        var viewport = new Rect(0, rowBounds.Top - 60, 480, 350);
        selection.Begin(new Point(450, rowBounds.Bottom + 25), viewport, false);
        selection.Update(new Point(300, rowBounds.Top + 1));
        Expect(grid, items[2], items[3]);
        selection.Update(new Point(300, rowBounds.Bottom + 1));
        Expect(grid, items[3]); // Shrinking the rectangle must deselect rows.
        selection.Finish();
        Expect(grid, items[3]); // Releasing leaves a group ready for the existing export drag.
        selection.Begin(new Point(450, rowBounds.Bottom - 1), viewport, true);
        selection.Update(new Point(300, viewport.Top));
        Expect(grid, items[1], items[2], items[3]); // Ctrl adds, and the parent row is excluded.
        selection.Finish();
        selection.Begin(new Point(450, rowBounds.Bottom + 25), viewport, false);
        selection.Update(new Point(300, rowBounds.Bottom + 1));
        grid.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(grid)!, 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
        Expect(grid, items[1], items[2], items[3]); // Escape restores the pre-gesture selection.
        grid.ItemsSource = new[] { items[2], items[4] }; // Filtered / sorted view, no stale item indexes.
        grid.UpdateLayout();
        row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(items[4]);
        rowBounds = row.TransformToAncestor(grid).TransformBounds(new Rect(row.RenderSize));
        selection.Begin(new Point(450, rowBounds.Bottom + 40), new Rect(0, 0, 480, 350), false);
        selection.Update(new Point(300, rowBounds.Top + 1));
        Expect(grid, items[4]);
        selection.Finish();
        window.Close();
        Console.WriteLine("PASS: rectangle selection, shrinking, Ctrl additions, parent exclusion, release persistence, Escape restoration and filtered view.");
    }

    private static ArchiveBrowserItem Item(string name, bool parent = false) => new(name, name, parent || name == "folder", parent, 0, 0, null, null, null);
    private static void Expect(DataGrid grid, params ArchiveBrowserItem[] expected)
    {
        if (!grid.SelectedItems.Cast<ArchiveBrowserItem>().ToHashSet().SetEquals(expected))
            throw new Exception("Unexpected selection: " + string.Join(", ", grid.SelectedItems.Cast<ArchiveBrowserItem>().Select(item => item.Name)));
    }
}


