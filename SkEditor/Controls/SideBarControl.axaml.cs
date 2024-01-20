using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace SkEditor.Controls;
public partial class SideBarControl : UserControl
{
    public SideBarControl()
    {
        InitializeComponent();

        AssignCommands();
    }

    private void AssignCommands()
    {
        ProjectsButton.Command = new RelayCommand(() =>
        {
            ScrollViewer.SetHorizontalScrollBarVisibility(FileTreeView, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(FileTreeView, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
            ExtendedSideBar.Width = ExtendedSideBar.Width == 0 ? 250 : 0;
        });
    }
}