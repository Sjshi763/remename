using Avalonia.Controls;
using remename.ViewModels;
using System;

namespace remename.Views;

public partial class MobileMainView : UserControl
{
    public MobileMainView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.FolderPickerService = new AvaloniaFolderPickerService(this);
        }
    }
}
